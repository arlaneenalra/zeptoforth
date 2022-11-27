\ Copyright (c) 2022 Travis Bemann
\ 
\ Permission is hereby granted, free of charge, to any person obtaining a copy
\ of this software and associated documentation files (the "Software"), to deal
\ in the Software without restriction, including without limitation the rights
\ to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
\ copies of the Software, and to permit persons to whom the Software is
\ furnished to do so, subject to the following conditions:
\ 
\ The above copyright notice and this permission notice shall be included in
\ all copies or substantial portions of the Software.
\ 
\ THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
\ IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
\ FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
\ AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
\ LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
\ OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
\ SOFTWARE.

begin-module balls

  oo import
  bitmap import
  ssd1306 import
  rng import
  systick import
  task import
  
  \ An I2C SSD1306 device
  <ssd1306> class-size buffer: my-ssd1306
  
  \ A backinb bitmap
  <bitmap> class-size buffer: my-sprite
  
  \ The number of columns
  128 constant my-cols

  \ The number of rows
  64 constant my-rows
  
  \ The width of the sprite
  12 constant my-sprite-cols
  
  \ The height of the sprite
  12 constant my-sprite-rows

  \ My framebuffer size
  my-cols my-rows bitmap-buf-size constant my-buf-size

  \ My framebuffer
  my-buf-size buffer: my-buf
  
  \ My sprite buffer size
  my-sprite-cols my-sprite-rows bitmap-buf-size constant my-sprite-buf-size
  
  \ My sprite buffer
  my-sprite-buf-size buffer: my-sprite-buf
  
  \ Has balls been initialized
  false value inited?

  \ Our balls' speed
  1,0 2constant my-ball-init-speed

  \ Our number of balls
  8 constant my-ball-count

  \ Frame delay in ticks
  500 constant my-frame-delay
  
  \ Our ball structure
  begin-structure ball-size

    \ Our ball's center's column
    2field: ball-col
    
    \ Our ball's center's row
    2field: ball-row

    \ Our ball's column delta
    2field: ball-delta-col

    \ Our ball's row delta
    2field: ball-delta-row

  end-structure

  \ Our array of balls
  ball-size my-ball-count * buffer: my-balls

  \ Generate a positive S31.32 value modulo a specified value
  : generate-ufmod { mod-lo mod-hi -- value-lo value-hi }
    random random mod-lo mod-hi uf/mod 2drop
  ;
  
  \ Initialize a ball
  : init-ball { ball -- }
    0 my-cols my-sprite-cols - generate-ufmod
    0 my-sprite-cols 2,0 f/ d+ ball ball-col 2!
    0 my-rows my-sprite-rows - generate-ufmod
    0 my-sprite-rows 2,0 f/ d+ ball ball-row 2!
    pi 2,0 f* generate-ufmod { ball-dir-lo ball-dir-hi }
    ball-dir-lo ball-dir-hi cos my-ball-init-speed f* ball ball-delta-col 2!
    ball-dir-lo ball-dir-hi sin my-ball-init-speed f* ball ball-delta-row 2!
  ;

  \ Initialize the display
  : init-display ( -- )
    14 15 my-buf my-cols my-rows SSD1306_I2C_ADDR 1
    <ssd1306> my-ssd1306 init-object
  ;

  \ Initialize the sprite
  : init-sprite ( -- )
    my-sprite-buf my-sprite-cols my-sprite-rows
    <bitmap> my-sprite init-object
    pi 2,0 f* { a-circle-lo a-circle-hi }
    a-circle-lo a-circle-hi
    0 my-sprite-cols 1,5 f* 0 my-sprite-rows 2,0 f* d+ f/
    { incr-lo incr-hi }
    0 my-sprite-cols 2,0 f/ { half-width-lo half-width-hi }
    0 my-sprite-rows 2,0 f/ { half-height-lo half-height-hi }
    0,0 { angle-lo angle-hi }
\    $FF 0 my-sprite-cols 0 my-sprite-rows my-sprite or-rect-const
    begin angle-lo angle-hi a-circle-lo a-circle-hi d<= while
      angle-lo angle-hi cos half-width-lo half-width-hi 1,0 d- f*
      half-width-lo half-width-hi d+ nip
      0 max my-sprite-cols min { pixel-col }
      angle-lo angle-hi sin half-height-lo half-height-hi 1,0 d- f*
      half-height-lo half-height-hi d+ nip
      0 max my-sprite-cols min { pixel-row }
      $FF pixel-col pixel-row my-sprite set-pixel-const
      \ cr ." angle: " angle-lo angle-hi f.
      \ ." col: " pixel-col .
      \ ." row: " pixel-row .
      \ ." mag: " 0 pixel-col half-width-lo half-width-hi d- 2 fi**
      \ 0 pixel-row half-height-lo half-height-hi d- 2 fi** d+ sqrt f.
      \ ." atan2: " 0 pixel-row half-height-lo half-height-hi d-
      \ 0 pixel-col half-width-lo half-width-hi d- atan2 f.
      angle-lo angle-hi incr-lo incr-hi d+ to angle-hi to angle-lo
    repeat
\    $FF 0 my-sprite-cols 0 my-sprite-rows my-sprite xor-rect-const
  ;

  \ Execute code for each of the balls
  : for-all-balls { xt -- }
    my-ball-count 0 ?do my-balls ball-size i * + xt execute loop
  ;

  \ Draw a ball
  : draw-ball { ball -- }
    ball ball-col 2@ nip my-sprite-cols 2 / - { sprite-col }
    ball ball-row 2@ nip my-sprite-rows 2 / - { sprite-row }
    0 sprite-col my-sprite-cols 0 sprite-row my-sprite-rows
    my-sprite my-ssd1306 xor-rect
  ;

  \ Move a ball
  : move-ball { ball -- }
    ball ball-delta-col 2@ ball ball-col 2+!
    ball ball-delta-row 2@ ball ball-row 2+!
  ;

  \ Bounce a ball
  : bounce-ball { ball -- }
    ball ball-col 2@ 0 my-sprite-cols 2,0 f/ d<= if
      ball ball-delta-col 2@ dabs ball ball-delta-col 2!
    else
      ball ball-col 2@ 0 my-cols 0 my-sprite-cols 2,0 f/ d- d>= if
        ball ball-delta-col 2@ dabs dnegate ball ball-delta-col 2!
      then
    then
    ball ball-row 2@ 0 my-sprite-rows 2,0 f/ d<= if
      ball ball-delta-row 2@ dabs ball ball-delta-row 2!
    else
      ball ball-row 2@ 0 my-rows 0 my-sprite-rows 2,0 f/ d- d>= if
        ball ball-delta-row 2@ dabs dnegate ball ball-delta-row 2!
      then
    then
  ;
  
  \ Run balls
  : run-balls ( -- )
    inited? not if
      init-display
      init-sprite
      ['] init-ball for-all-balls
      true to inited?
    then
    systick-counter { last-frame }
    begin key? not while
      ['] draw-ball for-all-balls
      my-ssd1306 update-display
      ['] draw-ball for-all-balls
      ['] move-ball for-all-balls
      ['] bounce-ball for-all-balls
      my-frame-delay last-frame current-task delay
      my-frame-delay +to last-frame
    repeat
    key drop
    my-ssd1306 clear-bitmap
    my-ssd1306 update-display
  ;
  
  
end-module
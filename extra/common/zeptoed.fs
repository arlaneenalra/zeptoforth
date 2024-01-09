\ Copyright (c) 2023-2024 Travis Bemann
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

begin-module zeptoed-internal

  oo import
  dyn-buffer import
  systick import
  heap import
  fat32 import
  ansi-term import

  \ DEBUG
  : *echo* [immediate] char lit, postpone internal::serial-emit ;
  \ DEBUG

  \ Normal video
  : normal-video ( -- ) csi ." 0m" ;

  \ Inverse video
  : inverse-video ( -- ) csi ." 7m" ;
  
  \ Get whether a byte is the start of a unicode code point greater than 127
  : unicode-start? ( b -- flag ) $C0 and $C0 = ;

  \ Get whether a byte is part of a unicode code point greater than 127
  : unicode? ( b -- flag ) $80 and 0<> ;

  \ Generate file error message if one occurs
  : with-file-error ( xt -- c-addr u error? )
    try case
      ['] x-sector-size-not-supported of
        s" Sector sizes other than 512 are not supported" true
      endof
      ['] x-fs-version-not-supported of
        s" FAT32 version not supported" true
      endof
      ['] x-bad-info-sector of
        s" Bad info sector" true
      endof
      ['] x-no-clusters-free of
        s" No clusters free" true
      endof
      ['] x-file-name-format of
        s" Unsupported filename" true
      endof
      ['] x-out-of-range-entry of
        s" Out of range directory entry" true
      endof
      ['] x-out-of-range-partition of
        s" Out of range partition" true
      endof
      ['] x-entry-not-found of
        s" Directory entry not found" true
      endof
      ['] x-entry-already-exists of
        s" Directory entry already exists" true
      endof
      ['] x-entry-not-file of
        s" Directory entry not file" true
      endof
      ['] x-entry-not-dir of
        s" Directory entry not directory" true
      endof
      ['] x-forbidden-dir of
        s" Forbidden directory name" true
      endof
      ['] x-empty-path of
        s" Empty path" true
      endof
      ['] x-invalid-path of
        s" Invalid path" true
      endof
      ?raise 0 0 false 0
    endcase
  ;

  \ Character constants
  $09 constant tab
  $7F constant delete
  $0A constant newline
  $0D constant return
  $00 constant ctrl-space
  $01 constant ctrl-a
  $02 constant ctrl-b
  $05 constant ctrl-e
  $06 constant ctrl-f
  $0B constant ctrl-k
  $0E constant ctrl-n
  $0F constant ctrl-o
  $10 constant ctrl-p
  $15 constant ctrl-u
  $16 constant ctrl-v
  $17 constant ctrl-w
  $18 constant ctrl-x
  $19 constant ctrl-y
  $1A constant ctrl-z

  \ Tab size
  2 constant tab-size \ small to save space in the blocks

  \ My base heap size
  65536 constant my-base-heap-size

  \ Maximum undo count
  64 constant max-undo-count

  \ Maximum undo byte count
  1024 constant max-undo-byte-count

  \ Delete undo accumulation
  -20 constant delete-undo-accumulation
  
  \ My block size
  default-segment-size dyn-buffer-internal::segment-header-size +
  constant my-block-size

  \ My block count
  my-base-heap-size my-block-size / constant my-block-count
  
  \ My real heap size
  my-block-size my-block-count heap-size constant my-heap-size
  
  \ Display width
  variable display-width

  \ Display height
  variable display-height

  \ Tab character size
  variable tab-char-size

  \ Get the length of a string
  : string-len { chars addr bytes -- chars' }
    bytes 0 ?do
      addr i + c@ { byte }
      byte tab = if
        tab-char-size @ chars tab-char-size @ umod - +to chars
      else
        byte $20 >= byte $7F < and byte unicode-start? or if 1 +to chars then
      then
    loop
    chars
  ;

  \ Undo header
  begin-structure undo-header-size

    \ Size
    field: undo-size

    \ Offset
    field: undo-offset

  end-structure
          
  \ Buffer class
  <object> begin-class <buffer>

    \ Editor
    cell member buffer-editor
    
    \ Heap
    cell member buffer-heap
    
    \ Buffer name
    2 cells member buffer-name

    \ Buffer path
    2 cells member buffer-path

    \ Previous buffer
    cell member buffer-prev

    \ Next buffer
    cell member buffer-next

    \ Edit cursor column
    cell member buffer-edit-col
    
    \ Edit cursor row
    cell member buffer-edit-row

    \ Select enabled
    cell member buffer-select-enabled

    \ Is the buffer dirty
    cell member buffer-dirty

    \ Buffer dynamic buffer
    <dyn-buffer> class-size member buffer-dyn-buffer
    
    \ Display area cursor
    <cursor> class-size member buffer-display-cursor

    \ Edit cursor
    <cursor> class-size member buffer-edit-cursor

    \ Select cursor
    <cursor> class-size member buffer-select-cursor

    \ Undo count
    cell member buffer-undo-count

    \ Undo byte count
    cell member buffer-undo-bytes

    \ Undo array
    max-undo-count cells member buffer-undo-array

    \ Left bound index
    cell member buffer-left-bound

    \ Dirty buffer
    method dirty-buffer ( buffer -- )

    \ Clean buffer
    method clean-buffer ( buffer -- )

    \ Buffer width in characters
    method buffer-width@ ( buffer -- width )

    \ Buffer height in characters
    method buffer-height@ ( buffer -- height )

    \ Set buffer coordinate
    method set-buffer-coord ( buffer -- )
    
    \ Go to a buffer coordinate
    method go-to-buffer-coord ( row col buffer -- )

    \ Clear the undos
    method clear-undos ( buffer -- )
    
    \ Get the oldest undo
    method oldest-undo@ ( buffer -- undo )

    \ Get the newest undo
    method newest-undo@ ( buffer -- undo )

    \ Push an undo
    method push-undo ( bytes buffer -- undo )

    \ Drop an undo
    method drop-undo ( buffer -- )

    \ Free an undo
    method free-undo ( undo buffer -- )
    
    \ Make room for undos
    method ensure-undo-space ( bytes buffer -- )
    
    \ Add an insert undo
    method add-insert-undo ( start-offset end-offset insert-offset buffer -- )

    \ Add a delete undo
    method add-delete-undo ( bytes offset buffer -- )
    
    \ Get whether two cursors share the same preceding newline
    method cursors-before-same-newline? ( cursor0 cursor1 buffer -- same? )

    \ Get whether two cursors are on the same line
    method cursors-on-same-line? ( cursor0 cursor1 buffer -- same? )

    \ Get the distance between two offsets
    method char-dist ( init-chars offset0 offset1 buffer -- distance )
    
    \ Get the byte before a cursor
    method cursor-before ( orig-cursor buffer -- c|0 )

    \ Get the byte at a cursor
    method cursor-at ( orig-cursor buffer -- c|0 )

    \ Get the byte before the edit cursor
    method edit-cursor-before ( buffer -- c|0 )

    \ Get the byte at the edit cursor
    method edit-cursor-at ( buffer -- c|0 )

    \ Length in spaces of the character before the edit cursor
    method edit-cursor-before-spaces ( buffer -- spaces )

    \ Length in spaces of the character at the edit cursor
    method edit-cursor-at-spaces ( buffer -- spaces )

    \ Find the raw distance of a cursor from the previous newline
    method cursor-raw-start-dist ( orig-cursor buffer -- distance )
    
    \ Find the distance of a cursor from the previous newline
    method cursor-start-dist ( orig-cursor buffer -- distance )

    \ Find the raw distance of a cursor from the next newline
    method cursor-raw-end-dist ( orig-cursor buffer -- distance )

    \ Find the distance of a cursor from the next newline
    method cursor-end-dist ( orig-cursor buffer -- distance )

    \ Get a cursor's distance from the left-hand side of the display
    method cursor-left-space ( cursor buffer -- col row )

    \ Get the length of a line a cursor is on
    method cursor-line-len ( cursor buffer -- length )

    \ Get the number of rows making up a line
    method cursor-line-rows ( cursor buffer -- rows )
    
    \ Get the length of the last row of a line
    method cursor-line-last-row-len ( cursor buffer -- length )

    \ Is a cursor in the last row of a line?
    method cursor-line-last? ( cursor buffer -- last? )

    \ Edit cursor left space
    method edit-cursor-left-space ( buffer -- cols rows )

    \ Edit cursor offset
    method edit-cursor-offset@ ( buffer -- offset )

    \ Buffer content length
    method buffer-len@ ( buffer -- length )

    \ Are we at the start of a line?
    method edit-cursor-at-start? ( buffer -- start? )

    \ Are we at the start of a row?
    method edit-cursor-at-row-start? ( buffer -- row-start? )

    \ Are we at the end of a line?
    method edit-cursor-at-end? ( buffer -- end? )

    \ Are we at the end of a row?
    method edit-cursor-at-row-end? ( bufer -- row-end? )

    \ Is the edit cursor in the last row of a line
    method edit-cursor-line-last? ( buffer -- last? )

    \ Is the edit cursor in a single row line?
    method edit-cursor-single-row? ( buffer -- single-row? )

    \ Is the edit cursor at the start of the last row of a line
    method edit-cursor-line-last-first? ( buffer -- last-first? )

    \ Is the edit cursor in the first row of the first line
    method edit-cursor-first-row? ( buffer -- first-row? )

    \ Is the edit cursor in the last row of the last line
    method edit-cursor-last-row? ( buffer -- last-row? )

    \ Output buffer title
    method output-title ( buffer -- )
    
    \ Refresh the current line
    method refresh-line ( buffer -- )
    
    \ Refresh the display
    method refresh-display ( buffer -- )

    \ Center the display
    method center-cursor ( center-cursor dest-cursor buffer -- )

    \ Put a cursor on the same row as a cursor
    method same-row-cursor ( src-cursor dest-cursor buffer -- )

    \ Get the number of rows between two cursors
    method rows-between-cursors ( cursor0 cursor1 buffer -- rows )
    
    \ Update the display
    method update-display ( buffer -- update? )

    \ Display a single character
    method output-char ( c buffer -- )

    \ Backspace a single char
    method output-backspace ( buffer -- )

    \ Move the cursor to the left
    method output-left ( spaces buffer -- )

    \ Move the cursor to the end of the previous row
    method output-prev-row ( buffer -- )

    \ Move the cursor to the end of the previous line
    method output-prev-line ( buffer -- )

    \ Move the cursor to the right
    method output-right ( spaces buffer -- )

    \ Move the cursor to the start of the next row
    method output-next-row ( buffer -- )

    \ Move the cursor to the start of the next line
    method output-next-line ( buffer -- )

    \ Move the cursor to the last position in a buffer
    method output-last ( buffer -- )
    
    \ Move the cursor to the next line
    method output-down ( buffer -- )

    \ Go to the start of a Unicode character
    method go-to-unicode-start ( buffer -- )

    \ Go to the end of a Unicode character
    method go-to-unicode-end ( buffer -- )
    
    \ Go to the first position in a buffer
    method do-first ( buffer -- )

    \ Go to the last position in a buffer
    method do-last ( buffer -- )
    
    \ Move the edit cursor left by one character
    method do-backward ( buffer -- )

    \ Move the edit cursor right by one character
    method do-forward ( buffer -- )

    \ Move the edit cursor up by one character
    method do-up ( buffer -- )

    \ Move the edit cursor down by one character
    method do-down ( buffer -- )
    
    \ Enter a character into the buffer
    method do-insert ( c buffer -- )

    \ Find the begin and end offsets for a delete
    method find-delete-range ( buffer -- start-offset end-offset )

    \ Find the begin and end offsets for a delete forward
    method find-delete-forward-range ( buffer -- start-offset end-offset )

    \ Backspace in the buffer
    method do-delete ( buffer -- )

    \ Delete in the buffer
    method do-delete-forward ( buffer -- )

    \ Kill a range in the buffer
    method do-kill ( clip buffer -- )

    \ Copy a range in the buffer
    method do-copy ( clip buffer -- )

    \ Paste in the buffer
    method do-paste ( clip buffer -- )

    \ Is there a selection in the buffer
    method selected? ( buffer -- selected? )
    
    \ Select in the buffer
    method do-select ( buffer -- )

    \ Deselect in the buffer
    method do-deselect ( buffer -- )

    \ Undo in the buffer
    method do-undo ( buffer -- )

    \ Go to the start of the line in the buffer
    method do-line-start ( buffer -- )

    \ Go to the end of the line in the buffer
    method do-line-end ( buffer -- )
    
    \ Page up in buffer
    method do-page-up ( buffer -- )

    \ Page down in buffer
    method do-page-down ( buffer -- )

    \ Go left one character
    method handle-backward ( buffer -- )

    \ Go right one character
    method handle-forward ( buffer -- )

    \ Go up one character
    method handle-up ( buffer -- )

    \ Go down one character
    method handle-down ( buffer -- )
    
    \ Enter a character into the buffer
    method handle-insert ( c buffer -- )

    \ Enter a newline into the buffer
    method handle-newline ( buffer -- )

    \ Backspace in the buffer
    method handle-delete ( buffer -- )

    \ Delete in the buffer
    method handle-delete-forward ( buffer -- )

    \ Kill a range in the buffer
    method handle-kill ( buffer -- )

    \ Copy a range in the buffer
    method handle-copy ( buffer -- )

    \ Paste in the buffer
    method handle-paste ( buffer -- )
    
    \ Select in the buffer
    method handle-select ( buffer -- )

    \ Undo in the buffer
    method handle-undo ( buffer -- )

    \ Go to line start in the buffer
    method handle-start ( buffer -- )

    \ Go to line end in the buffer
    method handle-end ( buffer -- )

    \ Page up in buffer
    method handle-page-up ( buffer -- )

    \ Page down in buffer
    method handle-page-down ( buffer -- )
    
  end-class

  \ File buffer class
  <buffer> begin-class <file-buffer>

    \ The file being edited
    <fat32-file> class-size member buffer-file

    \ Access a file, creating or opening it
    method access-file ( path-addr path-bytes buffer -- )

    \ Load from file
    method load-buffer-from-file ( buffer -- )
    
    \ Write out a file
    method write-buffer-to-file ( buffer -- )

  end-class

  \ Minibuffer class
  <buffer> begin-class <minibuffer>

    \ Are we read-only
    cell member minibuffer-read-only

    \ Minibuffer callback
    cell member minibuffer-callback

    \ Minibuffer callback object
    cell member minibuffer-callback-object

    \ Clear minibuffer
    method clear-minibuffer ( minibuffer -- )

    \ Display message
    method set-message ( addr bytes minibuffer -- )
    
    \ Set prompt
    method set-prompt ( addr bytes object xt minibuffer -- )

    \ Get the length of the input in the minibuffer
    method minibuffer-input-len@ ( minibuffer -- bytes )

    \ Read data from the minibuffer
    method read-minibuffer ( addr bytes offset minibuffer -- bytes' )
    
  end-class
  
  \ Clipboard class
  <object> begin-class <clip>
    
    \ Clipboard dynamic buffer
    <dyn-buffer> class-size member clip-dyn-buffer

    \ Clipboard cursor
    <cursor> class-size member clip-cursor

    \ Get the clip size
    method clip-size@ ( clip -- bytes )
    
    \ Copy from a starting cursor to an ending cursor
    method copy-to-clip ( start-cursor end-cursor src-dyn-buffer clip -- )

    \ Insert from the clipboard at a cursor
    method insert-from-clip ( dest-cursor clip -- )
    
  end-class
  
  \ Buffer editor class
  <object> begin-class <editor>
    
    \ Heap
    cell member editor-heap
    
    \ First buffer
    cell member editor-first

    \ Last buffer
    cell member editor-last

    \ Current buffer
    cell member editor-current

    \ Minibuffer
    cell member editor-minibuffer

    \ Are we in the minibuffer?
    cell member editor-in-minibuffer

    \ Are we going to exit
    cell member editor-exit
    
    \ Clipboard
    <clip> class-size member editor-clip

    \ Refresh the editor
    method refresh-editor ( editor -- )

    \ Activate minibuffer
    method activate-minibuffer ( editor -- )

    \ Deactivate minibuffer
    method deactivate-minibuffer ( editor -- )

    \ Get currently active buffer
    method active-buffer@ ( editor -- buffer )

    \ Set the cursor for the current buffer
    method update-coord ( editor -- )

    \ Set the cursor if the buffer is not the current buffer
    method update-different-coord ( buffer editor -- )
    
    \ Go left one character
    method handle-editor-backward ( editor -- )

    \ Go right one character
    method handle-editor-forward ( editor -- )

    \ Go up one character
    method handle-editor-up ( editor -- )

    \ Go down one character
    method handle-editor-down ( editor -- )
    
    \ Enter a character into the editor
    method handle-editor-insert ( c editor -- )

    \ Enter a newline into the editor
    method handle-editor-newline ( editor -- )

    \ Backspace in the editor
    method handle-editor-delete ( editor -- )

    \ Delete in the editor
    method handle-editor-delete-forward ( editor -- )

    \ Kill a range in the editor
    method handle-editor-kill ( editor -- )

    \ Copy a range in the editor
    method handle-editor-copy ( editor -- )

    \ Paste in the editor
    method handle-editor-paste ( editor -- )
    
    \ Select in the editor
    method handle-editor-select ( editor -- )

    \ Undo in the editor
    method handle-editor-undo ( editor -- )

    \ Go to the previous buffer
    method handle-editor-prev ( editor -- )

    \ Go to the next buffer
    method handle-editor-next ( editor -- )

    \ Create a new buffer
    method handle-editor-new ( editor -- )

    \ Actually create a new buffer
    method do-editor-new ( editor -- )

    \ Write to file
    method handle-editor-write ( editor -- )

    \ Revert from file
    method handle-editor-revert ( editor -- )
    
    \ Handle exit
    method handle-editor-exit ( editor -- )

    \ Handle a prompted exit
    method do-editor-exit ( editor -- )

    \ Handle editor start
    method handle-editor-start ( editor -- )

    \ Handle editor end
    method handle-editor-end ( editor -- )
    
    \ Handle editor page up
    method handle-editor-page-up ( editor -- )

    \ Handle editor page down
    method handle-editor-page-down ( editor -- )

  end-class

  \ Implement buffers
  <buffer> begin-implement
    
    \ Constructor
    :noname { name-addr name-bytes heap editor buffer -- }
      buffer <object>->new
      editor buffer buffer-editor !
      heap buffer buffer-heap !
      name-bytes heap allocate { new-name-addr }
      name-addr new-name-addr name-bytes move
      name-bytes heap allocate { new-path-addr }
      name-addr new-path-addr name-bytes move
      new-name-addr name-bytes buffer buffer-name 2!
      new-path-addr name-bytes buffer buffer-path 2!
      0 buffer buffer-prev !
      0 buffer buffer-next !
      0 buffer buffer-edit-col !
      0 buffer buffer-edit-row !
      0 buffer buffer-undo-count !
      0 buffer buffer-undo-bytes !
      0 buffer buffer-left-bound !
      false buffer buffer-select-enabled !
      false buffer buffer-dirty !
      heap <dyn-buffer> buffer buffer-dyn-buffer init-object
      buffer buffer-dyn-buffer <cursor> buffer buffer-display-cursor init-object
      buffer buffer-dyn-buffer <cursor> buffer buffer-edit-cursor init-object
      buffer buffer-dyn-buffer <cursor> buffer buffer-select-cursor init-object
    ; define new

    \ Destructor
    :noname { buffer -- }
      buffer clear-undos
      buffer buffer-select-cursor destroy
      buffer buffer-edit-cursor destroy
      buffer buffer-display-cursor destroy
      buffer buffer-dyn-buffer destroy
      buffer buffer-name 2@ drop buffer buffer-heap @ free
      buffer buffer-path 2@ drop buffer buffer-heap @ free
      buffer <object>->destroy
    ; define destroy

    \ Dirty buffer
    :noname ( buffer -- )
      true swap buffer-dirty !
    ; define dirty-buffer

    \ Clean buffer
    :noname ( buffer -- )
      false swap buffer-dirty !
    ; define clean-buffer
    
    \ Buffer width in characters
    :noname ( buffer -- width )
      drop display-width @
    ; define buffer-width@

    \ Buffer height in characters
    :noname ( buffer --  height )
      drop display-height @ 2 -
    ; define buffer-height@

    \ Set buffer cursor
    :noname { buffer -- }
      buffer buffer-edit-row @ buffer buffer-edit-col @
      buffer go-to-buffer-coord
    ; define set-buffer-coord
    
    \ Go to buffer coordinate
    :noname ( row col buffer -- )
      drop swap 1+ swap go-to-coord
    ; define go-to-buffer-coord

    \ Clear the undos
    :noname { buffer -- }
      begin buffer newest-undo@ while buffer drop-undo repeat
    ; define clear-undos
    
    \ Get the oldest undo
    :noname { buffer -- undo }
      buffer buffer-undo-count @ 0> if
        buffer buffer-undo-array buffer buffer-undo-count @ 1- cells + @
      else
        0
      then
    ; define oldest-undo@

    \ Get the newest undo
    :noname { buffer -- undo }
      buffer buffer-undo-count @ 0> if
        buffer buffer-undo-array @
      else
        0
      then
    ; define newest-undo@

    \ Push an undo
    :noname { bytes buffer -- undo }
      bytes [ undo-header-size cell+ ] literal + { full-bytes }
      full-bytes buffer ensure-undo-space
      buffer buffer-undo-array buffer buffer-undo-array cell+
      buffer buffer-undo-count @ cells move
      full-bytes buffer buffer-undo-bytes +!
      1 buffer buffer-undo-count +!
      bytes undo-header-size + buffer buffer-heap @ allocate { undo }
      undo buffer buffer-undo-array !
      undo
    ; define push-undo

    \ Drop an undo
    :noname { buffer -- }
      buffer buffer-undo-count @ 0> if
        buffer newest-undo@ buffer free-undo
        buffer buffer-undo-array cell+ buffer buffer-undo-array
        buffer buffer-undo-count @ cells move
      then
    ; define drop-undo
    
    \ Free an undo
    :noname { undo buffer -- }
      undo undo-size @ 0 max [ undo-header-size cell+ ] literal + negate
      buffer buffer-undo-bytes +!
      -1 buffer buffer-undo-count +!
      undo buffer buffer-heap @ free
    ; define free-undo
    
    \ Make room for undos
    :noname { bytes buffer -- }
      begin
        max-undo-byte-count buffer buffer-undo-bytes @ bytes + <
        buffer buffer-undo-count @ 0> and
        buffer buffer-undo-count @ max-undo-count = or
      while
        buffer oldest-undo@ buffer free-undo
      repeat
    ; define ensure-undo-space

    \ Add an insert undo
    :noname ( start-offset end-offset insert-offset buffer -- )
      dup buffer-dyn-buffer <cursor> [:
        default-segment-size [:
          { start-offset end-offset insert-offset buffer cursor data }
          end-offset start-offset - { bytes }
          bytes buffer push-undo { undo }
          start-offset cursor go-to-offset
          0 { data-offset }
          begin data-offset bytes < while
            bytes data-offset - default-segment-size min { part-size }
            data part-size cursor read-data to part-size
            data undo undo-header-size + data-offset + part-size move
            part-size +to data-offset
          repeat
          bytes undo undo-size !
          insert-offset undo undo-offset !
        ;] with-allot
      ;] with-object
    ; define add-insert-undo

    \ Add a delete undo
    :noname { bytes offset buffer -- }
      buffer newest-undo@ { undo }
      undo if
        undo undo-size @ 0<
        undo undo-size @ bytes - delete-undo-accumulation >= and
        offset bytes - undo undo-offset @ = and
      else
        false
      then if
        bytes negate undo undo-size +!
      else
        0 buffer push-undo to undo
        bytes negate undo undo-size !
      then
      offset undo undo-offset !
    ; define add-delete-undo
    
    \ Get whether two cursors share the same preceding newline
    :noname ( cursor0 cursor1 buffer -- same? )
      buffer-dyn-buffer <cursor> [: { cursor0 cursor1 cursor }
        cursor0 cursor copy-cursor
        [: $0A = ;] cursor find-prev
        cursor offset@ { offset0 }
        cursor1 cursor copy-cursor
        [: $0A = ;] cursor find-prev
        cursor offset@ { offset1 }
        offset0 offset1 =
      ;] with-object
    ; define cursors-before-same-newline?

    \ Get whether two cursors are on the same line
    :noname { cursor0 cursor1 buffer -- same? }
      cursor0 cursor1 buffer cursors-before-same-newline? if
        cursor0 buffer cursor-start-dist buffer buffer-width@ u/
        cursor1 buffer cursor-start-dist buffer buffer-width@ u/ =
      else
        false
      then
    ; define cursors-on-same-line?

    \ Get the distance between two offsets
    :noname ( init-chars offset0 offset1 buffer -- distance )
      buffer-dyn-buffer <cursor> [:
        default-segment-size [: { init-chars offset0 offset1 cursor data }
          offset0 offset1 min { start-offset }
          offset0 offset1 max { end-offset }
          start-offset cursor go-to-offset
          init-chars { chars }
          begin
            end-offset cursor offset@ - default-segment-size min 0 max
            { part-size }
            part-size 0> if
              data part-size cursor read-data to part-size
              chars data part-size string-len to chars
              false
            else
              true
            then
          until
          chars init-chars - offset1 offset0 < if negate then
        ;] with-allot
      ;] with-object
    ; define char-dist

    \ Get the byte before a cursor
    :noname ( orig-cursor buffer -- c|0 )
      buffer-dyn-buffer <cursor> [: { orig-cursor cursor }
        orig-cursor offset@ 0> if
          orig-cursor cursor copy-cursor
          -1 cursor adjust-offset
          0 { W^ data }
          data 1 cursor read-data drop
          data c@
        else
          0
        then
      ;] with-object
    ; define cursor-before

    \ Get the byte at a cursor
    :noname ( orig-cursor buffer -- c|0 )
      dup buffer-dyn-buffer <cursor> [: { orig-cursor buffer cursor }
        orig-cursor offset@ buffer buffer-len@ < if
          orig-cursor cursor copy-cursor
          0 { W^ data }
          data 1 cursor read-data drop
          data c@
        else
          0
        then
      ;] with-object
    ; define cursor-at
    
    \ Get the byte before the edit cursor
    :noname ( buffer -- c|0 )
      dup buffer-edit-cursor swap cursor-before
    ; define edit-cursor-before

    \ Get the byte at the edit cursor
    :noname ( buffer -- c|0 )
      dup buffer-edit-cursor swap cursor-at
    ; define edit-cursor-at
    
    \ Length in spaces of the character before the edit cursor
    :noname ( buffer -- spaces )
      dup edit-cursor-before
      tab = if
        dup buffer-dyn-buffer <cursor> [: { buffer cursor }
          buffer buffer-edit-cursor cursor copy-cursor
          -1 cursor adjust-offset
          cursor buffer cursor-start-dist { init-chars }
          init-chars cursor offset@ buffer buffer-edit-cursor offset@
          buffer char-dist
        ;] with-object
      else
        drop 1
      then
    ; define edit-cursor-before-spaces

    \ Length in spaces of the character at the edit cursor
    :noname ( buffer -- spaces )
      dup edit-cursor-at tab = if
        dup buffer-dyn-buffer <cursor> [: { buffer cursor }
          buffer buffer-edit-cursor buffer cursor-end-dist { first-dist }
          buffer buffer-edit-cursor cursor copy-cursor
          1 cursor adjust-offset
          first-dist cursor buffer cursor-end-dist -
        ;] with-object
      else
        drop 1
      then
    ; define edit-cursor-at-spaces

    \ Find the raw distance of a cursor from the previous newline
    :noname ( orig-cursor buffer -- distance )
      buffer-dyn-buffer <cursor> [: { orig-cursor cursor }
        orig-cursor cursor copy-cursor
        cursor offset@ { orig-offset }
        [: $0A = ;] cursor find-prev
        orig-offset cursor offset@ -
      ;] with-object
    ; define cursor-raw-start-dist

    \ Find the distance of a cursor from the previous newline
    :noname ( orig-cursor buffer -- distance )
      dup buffer-dyn-buffer <cursor> [: { orig-cursor buffer cursor }
        orig-cursor cursor copy-cursor
        cursor offset@ { orig-offset }
        [: $0A = ;] cursor find-prev
        0 cursor offset@ orig-offset buffer char-dist
      ;] with-object
    ; define cursor-start-dist

    \ Find the raw distance of a cursor from the next newline
    :noname ( orig-cursor buffer -- distance )
      buffer-dyn-buffer <cursor> [: { orig-cursor cursor }
        orig-cursor cursor copy-cursor
        cursor offset@ { orig-offset }
        [: $0A = ;] cursor find-next
        cursor offset@ orig-offset -
      ;] with-object
    ; define cursor-raw-end-dist
    
    \ Find the distance of a cursor from the next newline
    :noname ( orig-cursor buffer -- distance )
      dup buffer-dyn-buffer <cursor> [: { orig-cursor buffer cursor }
        orig-cursor buffer cursor-start-dist { init-chars }
        orig-cursor cursor copy-cursor
        cursor offset@ { orig-offset }
        [: $0A = ;] cursor find-next
        init-chars orig-offset cursor offset@ buffer char-dist
      ;] with-object
    ; define cursor-end-dist

    \ Get a cursor's distance from the left-hand side of the display
    :noname { cursor buffer -- col row }
      cursor buffer cursor-start-dist dup buffer buffer-width@ umod
      swap buffer buffer-width@ u/
    ; define cursor-left-space

    \ Get the length of a line a cursor is on
    :noname ( cursor buffer -- length )
      2dup cursor-start-dist -rot cursor-end-dist +
    ; define cursor-line-len

    \ Get the number of rows making up a line
    :noname { cursor buffer -- rows }
      cursor buffer cursor-line-len { len }
      len buffer buffer-width@ u/
      len buffer buffer-width@ umod 0<> if 1+ then
    ; define cursor-line-rows
    
    \ Get the length of the last row of a line
    :noname { cursor buffer -- length }
      cursor buffer cursor-line-len buffer buffer-width@ umod
    ; define cursor-line-last-row-len

    \ Is a cursor in the last row of a line?
    :noname { cursor buffer -- last? }
      cursor buffer cursor-line-last-row-len { len }
      cursor buffer cursor-end-dist { dist }
      len buffer buffer-width@ < if dist len <= else dist 0= then
    ; define cursor-line-last?

    \ Edit cursor left space
    :noname ( buffer -- cols rows )
      dup buffer-edit-cursor swap cursor-left-space
    ; define edit-cursor-left-space

    \ Edit cursor offset
    :noname ( buffer -- offset )
      buffer-edit-cursor offset@
    ; define edit-cursor-offset@

    \ Buffer content length
    :noname ( buffer -- length )
      buffer-dyn-buffer dyn-buffer-len@
    ; define buffer-len@
    
    \ Are we at the start of a line?
    :noname ( buffer -- start? )
      dup buffer-edit-cursor swap cursor-start-dist 0=
    ; define edit-cursor-at-start?

    \ Are we at the start of a row?
    :noname ( buffer -- row-start? )
      dup buffer-edit-cursor swap cursor-left-space drop 0=
    ; define edit-cursor-at-row-start?

    \ Are we at the end of a line?
    :noname ( buffer -- end? )
      dup buffer-edit-cursor swap cursor-end-dist 0=
    ; define edit-cursor-at-end?

    \ Are we at the end of a row?
    :noname { buffer -- row-end? }
      buffer buffer-edit-cursor buffer cursor-left-space drop
      buffer buffer-width@ 1- =
    ; define edit-cursor-at-row-end?

    \ Is the edit cursor in the last row of a line
    :noname ( buffer -- last? )
      dup buffer-edit-cursor swap cursor-line-last?
    ; define edit-cursor-line-last?

    \ Is the edit cursor in a single row line?
    :noname { buffer -- single-row? }
      buffer buffer-edit-cursor buffer cursor-line-len buffer buffer-width@ <
    ; define edit-cursor-single-row?

    \ Is the edit cursor at the start of the last row of a line
    :noname ( buffer -- last-first? )
      dup edit-cursor-line-last? if
        dup buffer-edit-cursor swap cursor-left-space drop 0=
      else
        drop false
      then
    ; define edit-cursor-line-last-first?

    \ Is the edit cursor in the first row of the first line
    :noname ( buffer -- first-row? )
      dup buffer-edit-cursor 2dup swap cursor-left-space nip 0= -rot
      dup rot cursor-start-dist swap offset@ = and
    ; define edit-cursor-first-row?

    \ Is the edit cursor in the last row of the last line
    :noname { buffer -- last-row? }
      buffer buffer-edit-cursor offset@
      buffer buffer-edit-cursor buffer cursor-raw-end-dist +
      buffer buffer-dyn-buffer dyn-buffer-len@ = if
        buffer edit-cursor-line-last?
      else
        false
      then
    ; define edit-cursor-last-row?
    
    \ Output buffer title
    :noname { buffer -- }
      0 0 go-to-coord
      buffer buffer-dirty @ if -2 else 0 then { dirty-offset }
      buffer buffer-name 2@ dup buffer buffer-width@ dirty-offset + < if
        type buffer buffer-dirty @ if ."  *" then erase-end-of-line
      else
        dup buffer buffer-width@ dirty-offset + = if
          type buffer buffer-dirty @ if ."  *" then
        else
          + buffer buffer-width@ dirty-offset + -
          buffer buffer-width@ dirty-offset +
          buffer buffer-dirty @ if ."  *" then
        then
      then
    ; define output-title
    
    \ Refresh the current line
    :noname ( buffer -- )
      dup buffer-dyn-buffer <cursor> [:
        default-segment-size [: { buffer cursor data }
          buffer buffer-edit-cursor cursor copy-cursor
          [: $0A = ;] cursor find-prev
          cursor buffer cursor-raw-end-dist { end-dist }
          hide-cursor
          buffer buffer-edit-row @ 0 buffer go-to-buffer-coord
          end-dist { len }
          0 { chars }
          false { cols-set? }
          begin len 0> while
            len default-segment-size min { part-size }
            cursor offset@ { prev-offset }
            data part-size cursor read-data { real-size }
            real-size 0 ?do
              prev-offset i + buffer buffer-edit-cursor offset@ = if
                chars buffer buffer-edit-col !
                true to cols-set?
              then
              data i + c@ { byte }
              byte tab = if
                tab-char-size @ chars tab-char-size @ umod - { tab-spaces }
                tab-spaces spaces
                tab-spaces +to chars
              else
                byte emit
                byte $20 >= byte $7F < and
                byte unicode-start? or if
                  1 +to chars
                then
              then
            loop
            part-size negate +to len
          repeat
          cols-set? not if chars buffer buffer-edit-col ! then
          chars buffer buffer-width@ < if erase-end-of-line then
          buffer buffer-editor @ update-coord
          show-cursor
        ;] with-allot
      ;] with-object
    ; define refresh-line
    
    \ Refresh the display
    :noname ( buffer -- )
      default-segment-size [:
        over buffer-dyn-buffer <cursor> [: { buffer data data-cursor }
          buffer buffer-display-cursor offset@ { display-offset }
          display-offset data-cursor go-to-offset
          buffer buffer-height@ { rows-remaining }
          buffer buffer-width@ { cols-remaining }
          0 0 { edit-col edit-row }
          buffer buffer-edit-cursor offset@ { edit-offset }
          buffer buffer-select-cursor offset@ { select-offset }
          hide-cursor
          normal-video
          buffer output-title
          0 0 buffer go-to-buffer-coord
          buffer buffer-select-enabled @ if
            select-offset display-offset <= if
              inverse-video
            then
          then
          begin
            rows-remaining 0> if
              data default-segment-size data-cursor read-data { actual-bytes }
              data { current-data }
              actual-bytes 0> if
                begin
                  actual-bytes 0> rows-remaining 0> and if
                    buffer buffer-select-enabled @
                    select-offset display-offset = and if
                      edit-offset display-offset <= if
                        normal-video
                      else
                        inverse-video
                      then
                    then
                    edit-offset display-offset = if
                      buffer buffer-select-enabled @ if
                        select-offset display-offset <= if
                          normal-video
                        else
                          inverse-video
                        then
                      then
                      buffer buffer-height@ rows-remaining - to edit-row
                      buffer buffer-width@ cols-remaining - to edit-col
                    then
                    current-data c@ { byte }
                    byte $0A = if
                      erase-end-of-line cr
                      -1 +to rows-remaining
                      buffer buffer-width@ to cols-remaining
                    else
                      byte tab = if
                        buffer buffer-width@ cols-remaining - { chars }
                        tab-char-size @ chars tab-char-size @ umod -
                        { tab-spaces }
                        tab-spaces spaces
                        tab-spaces negate +to cols-remaining
                        begin cols-remaining 0 < while
                          rows-remaining 0 > if
                            -1 +to rows-remaining
                          then
                          buffer buffer-width@ +to cols-remaining
                        repeat
                      else
                        byte emit
                        byte $20 >= byte $7F < and
                        byte unicode-start? or if
                          cols-remaining 1 > if
                            -1 +to cols-remaining
                          else
                            -1 +to rows-remaining
                            buffer buffer-width@ to cols-remaining
                          then
                        then
                      then
                    then
                    -1 +to actual-bytes
                    1 +to current-data
                    1 +to display-offset
                    false
                  else
                    true
                  then
                until
                false
              else
                true
              then
            else
              true
            then
          until
          normal-video
          erase-end-of-line
          rows-remaining 1 > if
            rows-remaining 1- 0 ?do cr erase-end-of-line loop
          then
          edit-offset display-offset = if
            buffer buffer-height@ rows-remaining - to edit-row
            buffer buffer-width@ cols-remaining - to edit-col
          then
          edit-row buffer buffer-edit-row !
          edit-col buffer buffer-edit-col !
          buffer buffer-editor @ update-coord
          show-cursor
        ;] with-object
      ;] with-allot
    ; define refresh-display

    \ Center the display
    :noname ( center-cursor dest-cursor buffer -- )
      dup buffer-dyn-buffer <cursor> [:
        { center-cursor dest-cursor buffer cursor }
        center-cursor cursor copy-cursor
        [: $0A = ;] cursor find-prev
        0 { rows }
        begin
          rows buffer buffer-height@ 2 / < cursor offset@ 0> and
        while
          -1 cursor adjust-offset
          cursor offset@ { old-offset }
          [: $0A = ;] cursor find-prev
          cursor offset@ { new-offset }
          0 new-offset old-offset buffer char-dist { diff }
          diff 0> if
            diff buffer buffer-width@ u/
            diff buffer buffer-width@ umod 0> if 1+ then
          else
            1
          then { new-rows }
          rows new-rows + buffer buffer-height@ 2 / <= if
            new-rows +to rows
          else
            dest-cursor cursor copy-cursor
            diff buffer buffer-width@ umod negate cursor adjust-offset
            1 +to rows
            diff buffer buffer-width@ u/ 0> if
              buffer buffer-height@ 2 / rows -
              buffer buffer-width@ * negate cursor adjust-offset
              new-rows 1- +to rows
            then
          then
          cursor dest-cursor copy-cursor
        repeat
        cursor dest-cursor copy-cursor
      ;] with-object
    ; define center-cursor

    \ Put a cursor on the same row as a cursor
    :noname { src-cursor dest-cursor buffer -- }
      src-cursor dest-cursor copy-cursor
      dest-cursor buffer cursor-left-space drop { cols }
      begin cols 0> while
        dest-cursor buffer cursor-start-dist { start-dist }
        -1 dest-cursor adjust-offset
        dest-cursor buffer cursor-start-dist { new-dist }
        new-dist start-dist - +to cols
      repeat
    ; define same-row-cursor
    
    \ Get the number of rows between two cursors
    :noname ( cursor0 cursor1 buffer -- rows )
      dup buffer-dyn-buffer <cursor> [: { cursor0 cursor1 buffer cursor }
        cursor1 cursor copy-cursor
        [: $0A = ;] cursor find-prev
        0 { rows }
        begin
          cursor offset@ cursor0 offset@ >
        while
          -1 cursor adjust-offset
          cursor offset@ { old-offset }
          [: $0A = ;] cursor find-prev
          cursor offset@ { new-offset }
          0 new-offset old-offset buffer char-dist { diff }
          new-offset cursor0 offset@ >= if
            diff 0> if
              diff buffer buffer-width@ u/
              diff buffer buffer-width@ umod 0> if 1+ then
            else
              1
            then
            +to rows
          else
            0 cursor0 offset@ old-offset buffer char-dist to diff
            diff buffer buffer-width@ umod 0> if 1 +to rows then
            diff buffer buffer-width@ u/ +to rows
          then
        repeat
        rows
      ;] with-object
    ; define rows-between-cursors
    
    \ Update the display
    :noname { buffer -- update? }
      buffer buffer-edit-cursor { edit-cursor }
      buffer buffer-display-cursor { display-cursor }
      edit-cursor offset@ display-cursor offset@ < if
        edit-cursor display-cursor buffer center-cursor true
      else
        display-cursor edit-cursor buffer rows-between-cursors
        dup buffer buffer-height@ 3 / <
        swap buffer buffer-height@ 2 * 3 / > or if
          display-cursor offset@ { old-offset }
          edit-cursor display-cursor buffer center-cursor
          display-cursor offset@ { new-offset }
          old-offset new-offset <>
        else
          false
        then
      then
      buffer buffer-select-enabled @ or
    ; define update-display

    \ Display a single character
    :noname { c buffer -- }
      c emit
      1 buffer buffer-edit-col +!
      buffer buffer-editor @ update-coord
    ; define output-char

    \ Backspace a single char
    :noname { buffer -- }
      hide-cursor
      $08 emit
      space
      $08 emit
      show-cursor
      -1 buffer buffer-edit-col +!
      buffer buffer-editor @ update-coord
    ; define output-backspace

    \ Move the cursor to the left
    :noname { spaces buffer -- }
      spaces negate buffer buffer-edit-col +!
      buffer buffer-editor @ update-coord
    ; define output-left

    \ Move the cursor to the end of the previous row
    :noname { buffer -- }
      -1 buffer buffer-edit-row +!
      buffer buffer-width@ 1- buffer buffer-edit-col !
      buffer buffer-editor @ update-coord
    ; define output-prev-row

    \ Move the cursor to the end of the previous line
    :noname { buffer -- }
      -1 buffer buffer-edit-row +!
      buffer edit-cursor-left-space drop buffer buffer-edit-col !
      buffer buffer-editor @ update-coord
    ; define output-prev-line

    \ Move the cursor to the right
    :noname { spaces buffer -- }
      spaces buffer buffer-edit-col +!
      buffer buffer-editor @ update-coord
    ; define output-right

    \ Move the cursor to the start of the next row
    :noname { buffer -- }
      1 buffer buffer-edit-row +!
      0 buffer buffer-edit-col !
      buffer buffer-editor @ update-coord
    ; define output-next-row

    \ Move the cursor to the start of the next line
    :noname { buffer -- }
      1 buffer buffer-edit-row +!
      0 buffer buffer-edit-col !
      buffer buffer-editor @ update-coord
    ; define output-next-line

    \ Move the cursor to the last position in a buffer
    :noname { buffer -- }
      buffer edit-cursor-left-space { cols rows }
      buffer buffer-edit-cursor buffer cursor-end-dist { dist }
      cols dist + buffer buffer-edit-col !
      buffer buffer-editor @ update-coord
    ; define output-last
    
    \ Move the cursor to the next line
    :noname { buffer -- }
      buffer edit-cursor-left-space { cols rows }
      1 buffer buffer-edit-row +!
      cols buffer buffer-edit-col !
      buffer buffer-editor @ update-coord
    ; define output-down

    \ Go to the start of a Unicode character
    :noname { buffer -- }
      buffer edit-cursor-at { byte }
      byte unicode? byte unicode-start? not and if
        begin
          -1 buffer buffer-edit-cursor adjust-offset
          buffer buffer-edit-cursor offset@ 0> if
            buffer edit-cursor-at to byte
            byte unicode? not byte unicode-start? or
          else
            true
          then
        until
      then
    ; define go-to-unicode-start

    \ Go to the end of a Unicode character
    :noname { buffer -- }
      buffer edit-cursor-at { byte }
      byte unicode? byte unicode-start? not and if
        begin
          1 buffer buffer-edit-cursor adjust-offset
          buffer buffer-edit-cursor offset@ buffer buffer-len@ < if
            buffer edit-cursor-at to byte
            byte unicode? not byte unicode-start? or
          else
            true
          then
        until
      then
    ; define go-to-unicode-end
    
    \ Go to the first position in a buffer
    :noname ( buffer -- )
      dup buffer-left-bound @ ?dup if
        swap buffer-edit-cursor go-to-offset
      else
        buffer-edit-cursor go-to-start
      then
    ; define do-first

    \ Go to the last position in a buffer
    :noname ( buffer -- )
      dup buffer-dyn-buffer dyn-buffer-len@ swap buffer-edit-cursor go-to-offset
    ; define do-last
    
    \ Move the edit cursor left by one character
    :noname { buffer -- }
      buffer buffer-edit-cursor offset@ buffer buffer-left-bound @ > if
        -1 buffer buffer-edit-cursor adjust-offset
        buffer go-to-unicode-start
      then
    ; define do-backward

    \ Move the edit cursor right by one character
    :noname { buffer -- }
      1 buffer buffer-edit-cursor adjust-offset
      buffer go-to-unicode-end
    ; define do-forward

    \ Move the edit cursor up by one character
    :noname { buffer -- }
      buffer buffer-edit-cursor buffer cursor-start-dist { dist }
      dist buffer buffer-width@ <= if
        [: $0A = ;] buffer buffer-edit-cursor find-prev
        -1 buffer buffer-edit-cursor adjust-offset
        buffer buffer-edit-cursor buffer cursor-start-dist { len }
        len buffer buffer-width@ umod dup { last-len } dist >= if
          last-len negate dist + buffer buffer-edit-cursor adjust-offset
        then
      else
        buffer buffer-width@ negate buffer buffer-edit-cursor adjust-offset
      then
      buffer buffer-edit-cursor offset@ buffer buffer-left-bound @ < if
        buffer buffer-left-bound @ buffer buffer-edit-cursor go-to-offset
      then
    ; define do-up

    \ Move the edit cursor down by one character
    :noname { buffer -- }
      buffer buffer-edit-cursor buffer cursor-start-dist { dist }
      buffer buffer-edit-cursor buffer cursor-left-space { col row }
      buffer edit-cursor-line-last? if
        dist buffer buffer-width@ umod { last-len }
        [: $0A = ;] buffer buffer-edit-cursor find-next
        1 buffer buffer-edit-cursor adjust-offset
        buffer buffer-edit-cursor buffer cursor-line-len { next-len }
        last-len next-len min buffer buffer-edit-cursor adjust-offset
      else
        buffer buffer-edit-cursor buffer cursor-line-len { len }
        len dist - dist buffer buffer-width@ umod > if
          buffer buffer-width@ buffer buffer-edit-cursor adjust-offset
        else
          len dist - buffer buffer-edit-cursor adjust-offset
        then
      then
    ; define do-down
    
    \ Enter a character into the buffer
    :noname { W^ c buffer -- }
      buffer buffer-select-cursor offset@ buffer buffer-edit-cursor offset@ -
      { select-diff }
      c 1 buffer buffer-edit-cursor insert-data
      select-diff 0> if 1 buffer buffer-select-cursor adjust-offset then
      1 buffer buffer-edit-cursor offset@ buffer add-delete-undo
      buffer dirty-buffer
    ; define do-insert

    \ Find the begin and end offsets for a delete
    :noname ( buffer -- start-offset end-offset )
      dup buffer-dyn-buffer <cursor> [: { buffer cursor }
        buffer buffer-edit-cursor cursor copy-cursor
        cursor offset@ { begin-offset }
        begin
          cursor offset@ 0> if
            cursor buffer cursor-before { byte }
            -1 cursor adjust-offset
            byte unicode? not byte unicode-start? or
          else
            true
          then
        until
        cursor offset@ begin-offset
      ;] with-object
    ; define find-delete-range
    
    \ Find the begin and end offsets for a delete forward
    :noname ( buffer -- start-offset end-offset )
      dup buffer-dyn-buffer <cursor> [: { buffer cursor }
        buffer buffer-edit-cursor cursor copy-cursor
        cursor buffer cursor-at { byte }
        byte unicode? byte unicode-start? not and if
          begin
            -1 cursor adjust-offset
            cursor offset@ 0> if
              cursor buffer cursor-at to byte
              byte unicode? not byte unicode-start? or
            else
              true
            then
          until
        then
        cursor offset@ { begin-offset }
        0 { count }
        begin
          cursor offset@ buffer buffer-len@ < if
            1 cursor adjust-offset
            1 +to count
            cursor buffer cursor-at to byte
            byte unicode? not byte unicode-start? or
          else
            true
          then
        until
        begin-offset dup count +
      ;] with-object
    ; define find-delete-forward-range

    \ Delete in the buffer
    :noname { buffer -- }
      buffer buffer-left-bound @ buffer buffer-edit-cursor offset@ = if
        exit
      then
      buffer find-delete-range over buffer add-insert-undo
      buffer buffer-select-cursor offset@ buffer buffer-edit-cursor offset@ -
      { select-diff }
      begin
        buffer buffer-edit-cursor offset@ 0> if
          buffer edit-cursor-before { byte }
          1 buffer buffer-edit-cursor delete-data
          buffer dirty-buffer
          select-diff 0>= if -1 buffer buffer-select-cursor adjust-offset then
          byte unicode? not byte unicode-start? or
        else
          true
        then
      until
    ; define do-delete

    \ Delete forward in the buffer
    :noname { buffer -- }
      buffer find-delete-forward-range over negate buffer add-insert-undo
      buffer go-to-unicode-start
      buffer buffer-select-cursor offset@ buffer buffer-edit-cursor offset@ -
      { select-diff }
      begin
        buffer buffer-edit-cursor offset@ buffer buffer-len@ < if
          1 buffer buffer-edit-cursor adjust-offset
          1 buffer buffer-edit-cursor delete-data
          buffer dirty-buffer
          select-diff 0> if -1 buffer buffer-select-cursor adjust-offset then
          buffer edit-cursor-at { byte }
          byte unicode? not byte unicode-start? or
        else
          true
        then
      until
    ; define do-delete-forward

    \ Kill a range in the buffer
    :noname { clip buffer -- }
      buffer buffer-select-cursor { select }
      buffer buffer-edit-cursor { edit }
      select offset@ { select-offset }
      edit offset@ { edit-offset }
      select-offset edit-offset < if
        select-offset edit-offset select-offset
        buffer add-insert-undo
        select edit buffer buffer-dyn-buffer clip copy-to-clip
        edit-offset select-offset - edit delete-data
        buffer dirty-buffer
      else
        edit-offset select-offset < if
          edit-offset select-offset edit-offset
          buffer add-insert-undo
          edit select buffer buffer-dyn-buffer clip copy-to-clip
          select-offset edit-offset - select delete-data
          buffer dirty-buffer
        then
      then
    ; define do-kill

    \ Copy a range in the buffer
    :noname { clip buffer -- }
      buffer buffer-select-cursor { select }
      buffer buffer-edit-cursor { edit }
      select offset@ { select-offset }
      edit offset@ { edit-offset }
      select-offset edit-offset < if
        select edit buffer buffer-dyn-buffer clip copy-to-clip
      else
        edit-offset select-offset < if
          edit select buffer buffer-dyn-buffer clip copy-to-clip
        then
      then
    ; define do-copy

    \ Paste in the buffer
    :noname { clip buffer -- }
      buffer buffer-edit-cursor { edit }
      buffer buffer-select-cursor { select }
      edit offset@ { edit-offset }
      select offset@ edit-offset - { select-diff }
      edit clip insert-from-clip
      select-diff 0>= if
        edit offset@ edit-offset - buffer buffer-select-cursor adjust-offset
      then
      edit offset@ { new-edit-offset }
      new-edit-offset edit-offset - new-edit-offset buffer add-delete-undo
      clip clip-size@ 0> if buffer dirty-buffer then
    ; define do-paste

    \ Is there a selection in the buffer
    :noname ( buffer -- selected? )
      buffer-select-enabled @
    ; define selected?
    
    \ Select in the buffer
    :noname { buffer -- }
      true buffer buffer-select-enabled !
      buffer buffer-edit-cursor buffer buffer-select-cursor copy-cursor
    ; define do-select

    \ Deselect in the buffer
    :noname ( buffer -- )
      false swap buffer-select-enabled !
    ; define do-deselect

    \ Undo in the buffer
    :noname { buffer -- }
      buffer newest-undo@ { undo }
      undo if
        buffer buffer-select-cursor offset@ { select-offset }
        undo undo-size @ 0> if
          undo undo-offset @ 0> if
            undo undo-offset @ buffer buffer-edit-cursor go-to-offset
            undo undo-header-size + undo undo-size @
            buffer buffer-edit-cursor insert-data
            select-offset undo undo-offset @ > if
              undo undo-size @ buffer buffer-select-cursor adjust-offset
            then
          else
            undo undo-offset @ negate buffer buffer-edit-cursor go-to-offset
            undo undo-header-size + undo undo-size @
            buffer buffer-edit-cursor insert-data
            undo undo-size @ negate buffer buffer-edit-cursor adjust-offset
            select-offset undo undo-offset @ negate > if
              undo undo-size @ buffer buffer-select-cursor adjust-offset
            then
          then
        else
          undo undo-offset @ buffer buffer-edit-cursor go-to-offset
          undo undo-size @ negate buffer buffer-edit-cursor delete-data
          select-offset undo undo-offset @ > if
            undo undo-size @ buffer buffer-select-cursor adjust-offset
          then
        then
        buffer drop-undo
        buffer dirty-buffer
      then
    ; define do-undo

    \ Go to the start of the line in the buffer
    :noname { buffer -- }
      [: $0A = ;] buffer buffer-edit-cursor find-prev
      buffer buffer-edit-cursor offset@ buffer buffer-left-bound @ < if
        buffer buffer-left-bound @ buffer buffer-edit-cursor go-to-offset
      then
    ; define do-line-start

    \ Go to the end of the line in the buffer
    :noname { buffer -- }
      [: $0A = ;] buffer buffer-edit-cursor find-next
    ; define do-line-end

    \ Page up in buffer
    :noname { buffer -- }
      buffer buffer-height@ 0 ?do
        buffer buffer-edit-cursor offset@ buffer buffer-left-bound @ > if
          buffer do-up
        else
          unloop exit
        then
      loop
    ; define do-page-up

    \ Page down in buffer
    :noname { buffer -- }
      buffer buffer-height@ 0 ?do
        buffer buffer-edit-cursor offset@ buffer buffer-len@ < if
          buffer do-down
        else
          unloop exit
        then
      loop
    ; define do-page-down
    
    \ Editor constants
    0 constant in-middle
    1 constant in-last-line
    2 constant at-end
    3 constant at-last-first
    4 constant at-last-last
    5 constant in-single-row
    6 constant at-start
  
    \ Go left one character
    :noname { buffer -- }
      buffer edit-cursor-offset@ 0> if
        buffer edit-cursor-left-space { cols rows }
        buffer edit-cursor-before-spaces { spaces }
        buffer do-backward
        buffer update-display if
          buffer refresh-display
        else
          cols 0> if
            buffer edit-cursor-at tab <> if
              spaces buffer output-left
            else
              buffer edit-cursor-single-row? if
                buffer refresh-line
              else
                buffer refresh-display
              then
            then
          else
            buffer refresh-display
          then
        then
      then
    ; define handle-backward

    \ Go right one character
    :noname { buffer -- }
      buffer edit-cursor-offset@ buffer buffer-len@ < if
        buffer edit-cursor-left-space { cols rows }
        buffer edit-cursor-at-spaces { spaces }
        buffer edit-cursor-at-end? { at-end? }
        buffer edit-cursor-before tab = { before-tab? }
        buffer do-forward
        buffer update-display if
          buffer refresh-display
        else
          at-end? if
            buffer output-next-line
          else
            cols buffer buffer-width@ 1- < if
              before-tab? not if
                spaces buffer output-right
              else
                buffer edit-cursor-single-row? if
                  buffer refresh-line
                else
                  buffer refresh-display
                then
              then
            else
              buffer refresh-display
            then
          then
        then
      then
    ; define handle-forward

    \ Go up one character
    :noname { buffer -- }
      buffer edit-cursor-first-row? if
        buffer do-first
        buffer update-display drop
        buffer refresh-display
      else
        buffer do-up
        buffer update-display drop
        buffer refresh-display
      then
    ; define handle-up

    \ Go down one character
    :noname { buffer -- }
      buffer edit-cursor-last-row? if
        buffer do-last
        buffer update-display if
          buffer refresh-display
        else
          buffer output-last
        then
      else
        buffer do-down
        buffer update-display if
          buffer refresh-display
        else
          buffer output-down
        then
      then
    ; define handle-down
    
    \ Enter a character into the buffer
    :noname { c buffer -- }
      buffer edit-cursor-at-end? if
        buffer edit-cursor-single-row?
        buffer edit-cursor-at-row-end? not and if
          c tab <> c unicode? not and if
            at-end
          else
            in-single-row
          then
        else
          at-last-last
        then
      else
        buffer edit-cursor-single-row? if
          in-single-row
        else
          in-middle
        then
      then { position }
      c buffer do-insert
      buffer update-display if
        buffer refresh-display
      else
        position case
          at-last-last of buffer refresh-display endof
          in-middle of buffer refresh-display endof
          in-single-row of buffer refresh-line endof
          at-end of c buffer output-char endof
        endcase
      then
    ; define handle-insert

    \ Enter a newline into the buffer
    :noname { buffer -- }
      $0A buffer do-insert
      buffer update-display drop
      buffer refresh-display
    ; define handle-newline

    \ Backspace in the buffer
    :noname { buffer -- }
      buffer buffer-left-bound @ buffer buffer-edit-cursor offset@ = if
        exit
      then
      buffer edit-cursor-at-row-start? if
        at-start
      else
        buffer edit-cursor-at-end?
        buffer edit-cursor-at-row-end? and
        buffer edit-cursor-before tab <> and if
          at-end
        else
          buffer edit-cursor-single-row? if
            in-single-row
          else
            in-middle
          then
        then
      then { position }
      buffer do-delete
      buffer update-display if
        buffer refresh-display
      else
        position case
          in-middle of buffer refresh-display endof
          at-start of buffer refresh-display endof
          in-single-row of buffer refresh-line endof
          at-end of buffer output-backspace endof
        endcase
      then
    ; define handle-delete

    \ Delete in the buffer
    :noname { buffer -- }
      buffer edit-cursor-offset@ buffer buffer-len@ = if exit then
      buffer edit-cursor-single-row? if
        in-single-row
      else
        in-middle
      then { position }
      buffer do-delete-forward
      buffer update-display if
        buffer refresh-display
      else
        position case
          in-middle of buffer refresh-display endof
          in-single-row of buffer refresh-line endof
        endcase
      then
    ; define handle-delete-forward

    \ Kill a range in the buffer
    :noname { buffer -- }
      buffer selected? if
        buffer buffer-editor @ editor-clip buffer do-kill
        buffer do-deselect
        buffer update-display drop
        buffer refresh-display
      then
    ; define handle-kill

    \ Copy a range in the buffer
    :noname { buffer -- }
      buffer selected? if
        buffer buffer-editor @ editor-clip buffer do-copy
        buffer do-deselect
        buffer refresh-display
      then
    ; define handle-copy

    \ Paste in the buffer
    :noname { buffer -- }
      buffer buffer-editor @ editor-clip buffer do-paste
      buffer update-display drop
      buffer refresh-display
    ; define handle-paste

    \ Select in the buffer
    :noname { buffer -- }
      buffer selected? if
        buffer do-deselect
      else
        buffer do-select
      then
      buffer refresh-display
    ; define handle-select

    \ Undo in the buffer
    :noname { buffer -- }
      buffer do-undo
      buffer update-display drop
      buffer refresh-display
    ; define handle-undo

    \ Go to line start in the buffer
    :noname { buffer -- }
      buffer edit-cursor-single-row? if
        in-single-row
      else
        in-middle
      then { position }
      buffer do-line-start
      buffer update-display if
        buffer refresh-display
      else
        position case
          in-single-row of buffer refresh-line endof
          in-middle of buffer refresh-display endof
        endcase
      then
    ; define handle-start

    \ Go to line end in the buffer
    :noname { buffer -- }
      buffer edit-cursor-single-row? if
        in-single-row
      else
        in-middle
      then { position }
      buffer do-line-end
      buffer update-display if
        buffer refresh-display
      else
        position case
          in-single-row of buffer refresh-line endof
          in-middle of buffer refresh-display endof
        endcase
      then
    ; define handle-end

    \ Page up in buffer
    :noname { buffer -- }
      buffer do-page-up
      buffer update-display drop
      buffer refresh-display
    ; define handle-page-up

    \ Page down in buffer
    :noname { buffer -- }
      buffer do-page-down
      buffer update-display drop
      buffer refresh-display
    ; define handle-page-down

  end-implement

  \ Implement the file buffer
  <file-buffer> begin-implement

    \ Constructor
    :noname { path-addr path-bytes heap editor buffer -- }
      path-addr path-bytes buffer access-file
      path-addr path-bytes heap editor buffer <buffer>->new
      buffer load-buffer-from-file
    ; define new

    \ Destructor
    :noname { buffer }
      buffer buffer-file destroy
      buffer <buffer>->destroy
    ; define destroy

    \ Dirty buffer
    :noname { buffer -- }
      buffer buffer-dirty @ { old-dirty }
      buffer <buffer>->dirty-buffer
      old-dirty not if
        buffer output-title
        buffer buffer-editor @ update-coord
      then
    ; define dirty-buffer

    \ Clean buffer
    :noname { buffer -- }
      buffer buffer-dirty @ { old-dirty }
      buffer <buffer>->clean-buffer
      old-dirty if
        buffer output-title
        buffer buffer-editor @ update-coord
      then
    ; define clean-buffer

    \ Access a file, creating or opening it
    :noname { path-addr path-bytes buffer -- }
      path-addr path-bytes fat32-tools::current-fs@ root-path-exists? if
        buffer buffer-file path-addr path-bytes [:
          { file file-addr file-bytes dir }
          file-addr file-bytes file dir open-file
        ;] fat32-tools::current-fs@ with-root-path
      else
        buffer buffer-file path-addr path-bytes [:
          { file file-addr file-bytes dir }
          file-addr file-bytes file dir create-file
        ;] fat32-tools::current-fs@ with-root-path
      then
    ; define access-file

    \ Load buffer from file
    :noname ( buffer -- )
      512 [: { buffer data }
        buffer buffer-edit-cursor go-to-end
        buffer buffer-edit-cursor offset@
        buffer buffer-edit-cursor delete-data
        buffer buffer-select-cursor go-to-start
        buffer buffer-display-cursor go-to-start
        0 seek-set buffer buffer-file seek-file
        buffer buffer-file file-size@ { bytes }
        0 { bytes-read }
        begin bytes-read bytes < while
          bytes bytes-read - 512 min { part-size }
          data part-size buffer buffer-file read-file to part-size
          data part-size buffer buffer-edit-cursor insert-data
          part-size +to bytes-read
        repeat
        buffer buffer-edit-cursor go-to-start
        buffer clean-buffer
      ;] with-allot
    ; define load-buffer-from-file
    
    \ Write out a file
    :noname ( buffer -- )
      512 [:
        over buffer-dyn-buffer <cursor> [: { buffer data cursor }
          buffer buffer-len@ { bytes }
          0 { bytes-written }
          0 seek-set buffer buffer-file seek-file
          begin bytes-written bytes < while
            bytes bytes-written - 512 min { part-size }
            data part-size cursor read-data to part-size
            data part-size buffer buffer-file write-file to part-size
            part-size +to bytes-written
          repeat
          buffer buffer-file truncate-file
          fat32-tools::current-fs@ flush
          buffer clean-buffer
        ;] with-object
      ;] with-allot
    ; define write-buffer-to-file
    
  end-implement
  
  \ Implement the minibuffer
  <minibuffer> begin-implement

    \ Constructor
    :noname { heap editor minibuffer -- }
      s" " heap editor minibuffer <buffer>->new
      true minibuffer minibuffer-read-only !
      0 minibuffer minibuffer-callback !
      0 minibuffer minibuffer-callback-object !
    ; define new

    \ Clear minibuffer
    :noname ( minibuffer -- )
      s" " rot set-message
    ; define clear-minibuffer
    
    \ Display message
    :noname { addr bytes minibuffer -- }
      minibuffer clear-undos
      minibuffer buffer-edit-cursor go-to-end
      minibuffer buffer-edit-cursor offset@
      minibuffer buffer-edit-cursor delete-data
      addr bytes minibuffer buffer-edit-cursor insert-data
      minibuffer buffer-select-cursor go-to-end
      minibuffer buffer-display-cursor go-to-start
      bytes minibuffer buffer-left-bound !
      true minibuffer minibuffer-read-only !
      0 minibuffer buffer-edit-row !
      minibuffer edit-cursor-left-space drop minibuffer buffer-edit-col !
      0 minibuffer minibuffer-callback !
      0 minibuffer minibuffer-callback-object !
      minibuffer clean-buffer
      minibuffer update-display drop
      minibuffer refresh-display
    ; define set-message
    
    \ Set prompt
    :noname { addr bytes object xt minibuffer -- }
      addr bytes minibuffer set-message
      false minibuffer minibuffer-read-only !
      xt minibuffer minibuffer-callback !
      object minibuffer minibuffer-callback-object !
    ; define set-prompt

    \ Get the length of the input in the minibuffer
    :noname ( minibuffer -- bytes )
      dup buffer-dyn-buffer <cursor> [: { minibuffer cursor }
        minibuffer buffer-edit-cursor cursor copy-cursor
        cursor go-to-end
        cursor offset@ minibuffer buffer-left-bound @ -
      ;] with-object
    ; define minibuffer-input-len@

    \ Read data from the minibuffer
    :noname ( addr bytes offset minibuffer -- bytes' )
      dup buffer-dyn-buffer <cursor> [: { addr bytes offset minibuffer cursor }
        minibuffer buffer-left-bound @ offset + cursor go-to-offset
        addr bytes cursor read-data
      ;] with-object
    ; define read-minibuffer
    
    \ Buffer height in characters
    :noname ( buffer -- height )
      drop 1
    ; define buffer-height@

    \ Go to buffer coordinate
    :noname ( row col buffer -- )
      drop nip display-height @ 1- swap go-to-coord
    ; define go-to-buffer-coord

    \ Output buffer title
    :noname ( buffer -- )
      drop
    ; define output-title
    
    \ Go left one character
    :noname ( buffer -- )
      dup minibuffer-read-only @ not if
        <buffer>->handle-backward
      else
        drop
      then
    ; define handle-backward

    \ Go right one character
    :noname ( buffer -- )
      dup minibuffer-read-only @ not if
        <buffer>->handle-forward
      else
        drop
      then
    ; define handle-forward

    \ Go up one character
    :noname ( buffer -- )
      dup minibuffer-read-only @ not if
        <buffer>->handle-up
      else
        drop
      then
    ; define handle-up

    \ Go down one character
    :noname ( buffer -- )
      dup minibuffer-read-only @ not if
        <buffer>->handle-down
      else
        drop
      then
    ; define handle-down
    
    \ Enter a character into the buffer
    :noname ( c buffer -- )
      dup minibuffer-read-only @ not if
        <buffer>->handle-insert
      else
        2drop
      then
    ; define handle-insert

    \ Enter a newline into the buffer
    :noname ( buffer -- )
      dup minibuffer-read-only @ not if
        dup minibuffer-callback @ ?dup if
          swap minibuffer-callback-object @ swap execute
        then
      else
        drop
      then
    ; define handle-newline

    \ Backspace in the buffer
    :noname ( buffer -- )
      dup minibuffer-read-only @ not if
        <buffer>->handle-delete
      else
        drop
      then
    ; define handle-delete

    \ Delete in the buffer
    :noname ( buffer -- )
      dup minibuffer-read-only @ not if
        <buffer>->handle-delete-forward
      else
        drop
      then
    ; define handle-delete-forward

    \ Kill a range in the buffer
    :noname ( buffer -- )
      dup minibuffer-read-only @ not if
        <buffer>->handle-kill
      else
        drop
      then
    ; define handle-kill

    \ Copy a range in the buffer
    :noname ( buffer -- )
      dup minibuffer-read-only @ not if
        <buffer>->handle-copy
      else
        drop
      then
    ; define handle-copy

    \ Paste in the buffer
    :noname ( buffer -- )
      dup minibuffer-read-only @ not if
        <buffer>->handle-paste
      else
        drop
      then
    ; define handle-paste
    
    \ Select in the buffer
    :noname ( buffer -- )
      dup minibuffer-read-only @ not if
        <buffer>->handle-select
      else
        drop
      then
    ; define handle-select

    \ Undo in the buffer
    :noname ( buffer -- )
      dup minibuffer-read-only @ not if
        <buffer>->handle-undo
      else
        drop
      then
    ; define handle-undo

  end-implement
  
  \ Implement the clipboard
  <clip> begin-implement

    \ Constructor
    :noname { heap clip -- }
      clip <object>->new
      heap <dyn-buffer> clip clip-dyn-buffer init-object
      clip clip-dyn-buffer <cursor> clip clip-cursor init-object
    ; define new

    \ Destructor
    :noname { clip -- }
      clip clip-cursor destroy
      clip clip-dyn-buffer destroy
      clip <object>->destroy
    ; define destroy

    \ Get the clip size
    :noname { clip -- bytes }
      clip clip-cursor offset@
    ; define clip-size@

    \ Copy from a starting cursor to an ending cursor
    :noname ( start-cursor end-cursor src-dyn-buffer clip -- )
      default-segment-size [:
        2 pick <cursor> [:
          { start-cursor end-cursor src-dyn-buffer clip buffer src-cursor }
          clip clip-cursor offset@ clip clip-cursor delete-data
          start-cursor src-cursor copy-cursor
          begin
            end-cursor offset@ src-cursor offset@ - default-segment-size min
            { part-size }
            part-size 0> if
              buffer part-size src-cursor read-data to part-size
              buffer part-size clip clip-cursor insert-data
              false
            else
              true
            then
          until
        ;] with-object
      ;] with-allot
    ; define copy-to-clip

    \ Insert from the clipboard at a cursor
    :noname ( dest-cursor clip -- )
      default-segment-size [: { dest-cursor clip buffer }
        clip clip-cursor { src-cursor }
        src-cursor offset@ { end-offset }
        src-cursor go-to-start
        begin
          end-offset src-cursor offset@ - default-segment-size min { part-size }
          part-size 0> if
            buffer part-size src-cursor read-data to part-size
            buffer part-size dest-cursor insert-data
            false
          else
            true
          then
        until
      ;] with-allot
    ; define insert-from-clip
    
  end-implement

  \ Implement the editor
  <editor> begin-implement

    \ Constructor
    :noname { path-addr path-bytes heap editor -- }
      editor <object>->new
      heap editor editor-heap !
      heap <clip> editor editor-clip init-object
      <file-buffer> class-size heap allocate { buffer }
      path-addr path-bytes heap editor <file-buffer> buffer init-object
      <minibuffer> class-size heap allocate { minibuffer }
      heap editor <minibuffer> minibuffer init-object
      buffer editor editor-first !
      buffer editor editor-last !
      buffer editor editor-current !
      minibuffer editor editor-minibuffer !
      false editor editor-in-minibuffer !
      false editor editor-exit !
    ; define new

    \ Destructor
    :noname { editor -- }
      editor editor-minibuffer @ destroy
      editor editor-minibuffer @ editor editor-heap @ free
      editor editor-current @ destroy
      editor editor-current @ editor editor-heap @ free
      editor <object>->destroy
    ; define destroy

    \ Refresh the editor
    :noname { editor -- }
      editor editor-current @ refresh-display
      editor editor-minibuffer @ refresh-display
    ; define refresh-editor

    \ Activate minibuffer
    :noname ( editor -- )
      true swap editor-in-minibuffer !
    ; define activate-minibuffer

    \ Deactivate minibuffer
    :noname ( editor -- )
      false swap editor-in-minibuffer !
    ; define deactivate-minibuffer

    \ Get currently active buffer
    :noname ( editor -- buffer )
      dup editor-in-minibuffer @ if
        editor-minibuffer @
      else
        editor-current @
      then
    ; define active-buffer@

    \ Set the cursor for the current buffer
    :noname ( editor -- )
      active-buffer@ set-buffer-coord
    ; define update-coord

    \ Set the cursor if the buffer is not the current buffer
    :noname { buffer editor -- }
      buffer editor active-buffer@ <> if
        editor active-buffer@ set-buffer-coord
      then
    ; define update-different-coord
    
    \ Go left one character
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-backward
      else
        editor editor-current @ handle-backward
      then
    ; define handle-editor-backward

    \ Go right one character
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-forward
      else
        editor editor-current @ handle-forward
      then
    ; define handle-editor-forward

    \ Go up one character
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-up
      else
        editor editor-current @ handle-up
      then
    ; define handle-editor-up

    \ Go down one character
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-down
      else
        editor editor-current @ handle-down
      then
    ; define handle-editor-down
    
    \ Enter a character into the editor
    :noname { c editor -- }
      editor editor-in-minibuffer @ if
        c editor editor-minibuffer @ handle-insert
      else
        c editor editor-current @ handle-insert
      then
    ; define handle-editor-insert

    \ Enter a newline into the editor
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-newline
      else
        editor editor-current @ handle-newline
      then
    ; define handle-editor-newline

    \ Backspace in the editor
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-delete
      else
        editor editor-current @ handle-delete
      then
    ; define handle-editor-delete

    \ Delete in the editor
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-delete-forward
      else
        editor editor-current @ handle-delete-forward
      then
    ; define handle-editor-delete-forward

    \ Kill a range in the editor
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-kill
      else
        editor editor-current @ handle-kill
      then
    ; define handle-editor-kill

    \ Copy a range in the editor
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-copy
      else
        editor editor-current @ handle-copy
      then
    ; define handle-editor-copy

    \ Paste in the editor
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-paste
      else
        editor editor-current @ handle-paste
      then
    ; define handle-editor-paste

    \ Select in the editor
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-select
      else
        editor editor-current @ handle-select
      then
    ; define handle-editor-select

    \ Undo in the editor
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-undo
      else
        editor editor-current @ handle-undo
      then
    ; define handle-editor-undo

    \ Go to the previous buffer
    :noname { editor -- }
      editor editor-in-minibuffer @ not if
        editor editor-current @ ?dup if
          buffer-prev @ ?dup if
            dup editor editor-current !
            refresh-display
          then
        then
      then
    ; define handle-editor-prev

    \ Go to the next buffer
    :noname { editor -- }
      editor editor-in-minibuffer @ not if
        editor editor-current @ ?dup if
          buffer-next @ ?dup if
            dup editor editor-current !
            refresh-display
          then
        then
      then
    ; define handle-editor-next

    \ Create a new buffer
    :noname { editor -- }
      editor editor-in-minibuffer @ not if
        editor activate-minibuffer
        s" New editor: " editor ['] do-editor-new
        editor editor-minibuffer @ set-prompt
      then
    ; define handle-editor-new

    \ Actually create a new buffer
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor 256 [: { editor data }
          data 256 0 editor editor-minibuffer @ read-minibuffer { len }
          <file-buffer> class-size editor editor-heap @ allocate { buffer }
          data len editor editor-heap @ editor <file-buffer> buffer
          ['] init-object with-file-error if
            buffer editor editor-heap @ free
            editor editor-minibuffer @ set-message
          else
            editor editor-current @ buffer buffer-prev !
            editor editor-current @ buffer-next @ buffer buffer-next !
            buffer buffer-next @ if
              buffer buffer-next @ buffer-prev !
            else
              buffer editor editor-last !
            then
            buffer editor editor-current @ buffer-next !
            buffer editor editor-current !
            editor editor-minibuffer @ clear-minibuffer
          then
        ;] with-allot
        editor deactivate-minibuffer
        editor editor-current @ refresh-display
      then
    ; define do-editor-new

    \ Handle write
    :noname { editor -- }
      editor editor-in-minibuffer @ not if
        editor editor-current @ if
          editor editor-current @ write-buffer-to-file
          editor editor-current @ refresh-display
          s" Wrote file" editor editor-minibuffer @ set-message
        then
      then
    ; define handle-editor-write

    \ Handle revert
    :noname { editor -- }
      editor editor-in-minibuffer @ not if
        editor editor-current @ if
          editor editor-current @ load-buffer-from-file
          editor editor-current @ refresh-display
          s" Reverted file" editor editor-minibuffer @ set-message
        then
      then
    ; define handle-editor-revert

    \ Handle exit
    :noname { editor -- }
      false editor editor-first @ begin ?dup while
        dup buffer-dirty @ rot or swap buffer-next @
      repeat
      if
        editor activate-minibuffer
        s" A buffer is modified, exit? (yes/no): " editor ['] do-editor-exit
        editor editor-minibuffer @ set-prompt
      else
        true editor editor-exit !
      then
    ; define handle-editor-exit

    \ Handle a prompted exit
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor 256 [: { editor data }
          data 256 0 editor editor-minibuffer @ read-minibuffer { len }
          begin
            len 0> if
              data c@ { byte }
              byte $20 <= byte $7F = or if
                1 +to data
                -1 +to len
                false
              else
                true
              then
            else
              true
            then
          until
          data c@ { byte }
          byte [char] Y = byte [char] y = or if
            true editor editor-exit ! true
          else
            byte [char] N = byte [char] n = or
          then
          if
            editor editor-minibuffer @ clear-minibuffer
            editor deactivate-minibuffer
            editor editor-current @ refresh-display
          then
        ;] with-allot
      then
    ; define do-editor-exit

    \ Handle editor start
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-start
      else
        editor editor-current @ handle-start
      then
    ; define handle-editor-start

    \ Handle editor end
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-end
      else
        editor editor-current @ handle-end
      then
    ; define handle-editor-end

    \ Handle editor page up
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-page-up
      else
        editor editor-current @ handle-page-up
      then
    ; define handle-editor-page-up

    \ Handle editor page down
    :noname { editor -- }
      editor editor-in-minibuffer @ if
        editor editor-minibuffer @ handle-page-down
      else
        editor editor-current @ handle-page-down
      then
    ; define handle-editor-page-down

  end-implement

  \ The line editor
  variable edit-state
  
  \ Configure the editor
  : config-edit ( -- )
    reset-ansi-term
    get-terminal-size display-width ! display-height !
    8 tab-char-size !
  ;

  \ Handle a special key
  : handle-special { editor -- }
    get-key case
      [char] A of editor handle-editor-up endof
      [char] B of editor handle-editor-down endof
      [char] C of editor handle-editor-forward endof
      [char] D of editor handle-editor-backward endof
      [char] 3 of
	get-key case
	  [char] ~ of editor handle-editor-delete-forward endof
	  clear-keys
	endcase
      endof
      [char] 5 of
        get-key case
          [char] ~ of editor handle-editor-page-up endof
          clear-keys
        endcase
      endof
      [char] 6 of
        get-key case
          [char] ~ of editor handle-editor-page-down endof
          clear-keys
        endcase
      endof
      clear-keys
    endcase
  ;

  \ Handle the escape key
  : handle-escape { editor -- }
    get-key case
      ctrl-k of editor handle-editor-copy endof
      [char] [ of editor handle-special endof
      clear-keys
    endcase
  ;

  \ Edit one or more files in a FAT32 filesystem
  : zeptoed ( path-addr path-bytes -- )
    fat32-tools::current-fs@ averts fat32-tools::x-fs-not-set
    my-heap-size [: { path-addr path-bytes my-heap }
      my-block-size my-block-count my-heap init-heap
      config-edit
      <editor> class-size my-heap allocate { editor }
      path-addr path-bytes my-heap <editor> editor
      ['] init-object with-file-error if
        cr type 2drop 2drop drop cr exit
      then
      editor refresh-editor
      begin editor editor-exit @ not while
        get-key
        dup $20 u< over tab <> and if
          case
            return of editor handle-editor-newline endof
            newline of editor handle-editor-newline endof
            \	    tab of handle-editor-tab endof
            ctrl-space of editor handle-editor-select endof
            ctrl-a of editor handle-editor-start endof
            ctrl-e of editor handle-editor-end endof
            ctrl-f of editor handle-editor-forward endof
            ctrl-b of editor handle-editor-backward endof
            ctrl-n of editor handle-editor-next endof
            ctrl-p of editor handle-editor-prev endof
            ctrl-o of editor handle-editor-new endof
            ctrl-v of editor handle-editor-exit endof
            ctrl-w of editor handle-editor-write endof
            ctrl-x of editor handle-editor-revert endof
            \	    ctrl-u of handle-editor-insert-row endof
            ctrl-k of editor handle-editor-kill endof
            ctrl-y of editor handle-editor-paste endof
            ctrl-z of editor handle-editor-undo endof
            escape of editor handle-escape endof
          endcase
          depth 1 < if ." *** " then \ DEBUG
        else
          dup delete = if
            drop editor handle-editor-delete
          else
            editor handle-editor-insert
          then
        then
      repeat
      display-height 0 go-to-coord cr
    ;] with-aligned-allot
  ;
  
end-module
 
\ Invoke zeptoed to edit a specified file in a FAT32 filesystem
: zeptoed ( path-addr path-bytes -- )
  zeptoed-internal::zeptoed
;
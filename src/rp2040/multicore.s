@ Copyright (c) 2021-2022 Travis Bemann
@
@ Permission is hereby granted, free of charge, to any person obtaining a copy
@ of this software and associated documentation files (the "Software"), to deal
@ in the Software without restriction, including without limitation the rights
@ to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
@ copies of the Software, and to permit persons to whom the Software is
@ furnished to do so, subject to the following conditions:
@ 
@ The above copyright notice and this permission notice shall be included in
@ all copies or substantial portions of the Software.
@ 
@ THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
@ IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
@ FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
@ AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
@ LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
@ OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
@ SOFTWARE.

	@@ CPUID register (do not confuse with the ARM CPUID register)
	.equ SIO_CPUID, SIO_BASE + 0x000

	@@ NVIC Base address
	.equ NVIC_Base, 0xE000E100

	@@ NVIC interrupt clear-pending register base address
	.equ NVIC_ICPR_Base, NVIC_Base + 0x180

	@@ FIFO status register
	.equ FIFO_ST, SIO_BASE + 0x050

	@@ Bit for core's read-on-empty flag
	.equ FIFO_ST_ROE, 1 << 3

	@@ Bit for core's write-on-empty flag
	.equ FIFO_ST_WOF, 1 << 2

	@@ Bit for core's TX FIFO is not full
	.equ FIFO_ST_RDY, 1 << 1

	@@ Bit for core's RX FIFO is not empty
	.equ FIFO_ST_VLD, 1 << 0

	@@ Write access to this core's TX FIFO
	.equ FIFO_WR, SIO_BASE + 0x054

	@@ Read access to this core's RX FIFO
	.equ FIFO_RD, SIO_BASE + 0x058

	@@ Synchronization spinlock index
	.equ SYNC_SPINLOCK_INDEX, 30

	@@ Spinlock 30, which we will use for synchronizing between
	.equ SYNC_SPINLOCK, SIO_BASE + 0x100 + (SYNC_SPINLOCK_INDEX * 4)

	@@ Synchronization value
	.equ SYNC_VALUE, 0x7FFFFFFF

	@@ Handle SIO interrupt
_handle_sio:
	push {lr}
	ldr r0, =begin_write
	ldr r1, [r0]
	cmp r1, #0
	beq 3f
	ldr r2, =waiting_write_done
	ldr r1, =-1
	str r1, [r2]
1:	ldr r1, [r0]
	cmp r1, #0
	beq 2f
	b 1b
2:	movs r1, #0
	str r1, [r2]
3:	ldr r0, =FIFO_ST
	ldr r0, [r0]
	movs r1, #FIFO_ST_VLD
	tst r0, r1
	beq 4f
	ldr r0, =FIFO_RD
	ldr r1, [r0]
	ldr r0, =SIO_CPUID
	ldr r2, [r0]
	lsls r2, r2, #2
	ldr r0, =sio_hook
	ldr r2, [r0, r2]
	cmp r2, #0
	beq 3b
	push_tos
	movs tos, r1
	adds r2, #1
	blx r2
	b 3b
4:	ldr r0, =FIFO_ST
	ldr r1, =FIFO_ST_ROE | FIFO_ST_WOF
	str r1, [r0]
	pop {pc}

	@@ Force the other core to wait
	define_internal_word "force-core-wait", visible_flag
_force_core_wait:
	ldr r0, =core_1_launched
	ldr r0, [r0]
	cmp r0, #0
	beq 2f
	ldr r0, =begin_write
	ldr r1, =-1
	str r1, [r0]
	ldr r2, =FIFO_WR
	str r1, [r2]
	ldr r2, =waiting_write_done
1:	ldr r1, [r2]
	cmp r1, #0
	beq 1b
	bx lr
	end_inlined

	@@ Release the other core
	define_internal_word "release-core", visible_flag
_release_core:	
	ldr r0, =core_1_launched
	ldr r0, [r0]
	cmp r0, #0
	beq 2f
	ldr r0, =begin_write
	movs r1, #0
	str r1, [r0]
	ldr r0, =waiting_write_done
1:	ldr r1, [r0]
	cmp r1, #0
	bne 1b
2:	bx lr
	end_inlined

	.ltorg

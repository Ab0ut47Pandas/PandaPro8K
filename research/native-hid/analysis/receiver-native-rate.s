/* Offline encoding check; never installed by this file. */
.text
.global rate_original
rate_original:
    r0 = 1
.global rate_unclamped
rate_unclamped:
    r0 = 0
.global native_fast_axes
native_fast_axes:
    r1 = r6 + 0x21f
    r3 = r6 + 0x50
    r2 = h[r3+0x0] (u)
    b[r5+0x8] = r2
    r2 = r2 >> 8
    b[r5+0x7] = r2
    r2 = h[r3+0x2] (u)
    b[r5+0xa] = r2
    r2 = r2 >> 8
    b[r5+0x9] = r2
    r2 = h[r3+0x4] (u)
    b[r5+0xc] = r2
    r2 = r2 >> 8
    b[r5+0xb] = r2
    r2 = h[r3+0x6] (u)
    b[r5+0xe] = r2
    r2 = r2 >> 8
    b[r5+0xd] = r2

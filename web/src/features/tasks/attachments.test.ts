import { afterEach, describe, expect, it, vi } from 'vitest'

import { downscaleImage } from './attachments'

function stubImage(width: number, height: number, encodedBytes = 100) {
  const drawn: number[] = []
  vi.stubGlobal('createImageBitmap', vi.fn().mockResolvedValue({ width, height, close: () => {} }))
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({
    drawImage: (_image: unknown, _x: number, _y: number, w: number, h: number) => drawn.push(w, h),
  } as unknown as CanvasRenderingContext2D)
  vi.spyOn(HTMLCanvasElement.prototype, 'toBlob').mockImplementation((callback, type, quality) => {
    expect(type).toBe('image/jpeg')
    expect(quality).toBe(0.85)
    callback(new Blob([new Uint8Array(encodedBytes)], { type: 'image/jpeg' }))
  })
  return drawn
}

const photo = (bytes: number, name = 'IMG_0001.HEIC', type = 'image/heic') =>
  new File([new Uint8Array(bytes)], name, { type })

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('downscaleImage', () => {
  it('shrinks big photos to 2000 px on the long edge as JPEG', async () => {
    const drawn = stubImage(4000, 3000)
    const result = await downscaleImage(photo(6_000_000))
    expect(drawn).toEqual([2000, 1500])
    expect(result.type).toBe('image/jpeg')
    expect(result.name).toBe('IMG_0001.jpg')
    expect(result.size).toBeLessThan(6_000_000)
  })

  it('keeps small photos as taken', async () => {
    stubImage(1200, 900)
    const original = photo(400_000, 'a.jpg', 'image/jpeg')
    expect(await downscaleImage(original)).toBe(original)
  })

  it('re-encodes a small-dimension photo that is still large', async () => {
    stubImage(1800, 1800, 300_000)
    const result = await downscaleImage(photo(3_000_000, 'a.png', 'image/png'))
    expect(result.name).toBe('a.jpg')
  })

  it('sends the original when the browser cannot decode it', async () => {
    vi.stubGlobal('createImageBitmap', vi.fn().mockRejectedValue(new Error('unsupported')))
    const original = photo(5_000_000)
    expect(await downscaleImage(original)).toBe(original)
  })

  it('leaves non-images alone', async () => {
    const pdf = photo(10, 'a.pdf', 'application/pdf')
    expect(await downscaleImage(pdf)).toBe(pdf)
  })
})

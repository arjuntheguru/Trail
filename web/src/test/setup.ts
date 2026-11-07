import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'

import { server } from './server'

beforeAll(() => server.listen({ onUnhandledFrame: 'error' }))
afterEach(() => {
  cleanup()
  server.resetHandlers()
})
afterAll(() => server.close())

// jsdom does not implement scrolling; the router calls it after navigations.
window.scrollTo = () => {}

// jsdom has no object URLs; photo thumbnails use them.
if (!URL.createObjectURL) {
  URL.createObjectURL = () => `blob:test/${Math.random().toString(36).slice(2)}`
  URL.revokeObjectURL = () => {}
}

// jsdom's Blob lacks stream(), which Node's fetch needs to send multipart uploads.
const streamOf = (blob: Blob) =>
  new ReadableStream<Uint8Array>({
    async start(controller) {
      controller.enqueue(new Uint8Array(await blob.arrayBuffer()))
      controller.close()
    },
  })
if (!Blob.prototype.stream) {
  Blob.prototype.stream = function (this: Blob) {
    return streamOf(this) as ReturnType<Blob['stream']>
  }
}

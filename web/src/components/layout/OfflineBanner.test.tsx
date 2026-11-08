import { onlineManager } from '@tanstack/react-query'
import { act, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'

import { OfflineBanner } from './OfflineBanner'

afterEach(() => onlineManager.setOnline(true))

describe('OfflineBanner', () => {
  it('shows only while offline', () => {
    render(<OfflineBanner />)
    expect(screen.queryByRole('status')).not.toBeInTheDocument()

    act(() => onlineManager.setOnline(false))
    expect(screen.getByRole('status')).toHaveTextContent(
      "You're offline. Showing saved data; changes you make will sync when you reconnect.",
    )

    act(() => onlineManager.setOnline(true))
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })
})

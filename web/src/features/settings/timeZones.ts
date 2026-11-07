/** The browser's own zone, a sensible default for a new site or organization. */
export function browserTimeZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC'
  } catch {
    return 'UTC'
  }
}

/** IANA ids the runtime knows, always including `current` so an existing value stays selectable. */
export function supportedTimeZones(current?: string): string[] {
  let zones: string[] = []
  try {
    zones = Intl.supportedValuesOf('timeZone')
  } catch {
    // Older runtimes lack supportedValuesOf; fall back to the zones we can name.
  }
  const all = new Set([...zones, 'UTC', browserTimeZone()])
  if (current) all.add(current)
  return [...all].sort((a, b) => a.localeCompare(b))
}

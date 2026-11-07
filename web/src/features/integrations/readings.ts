/** Where devices post readings, as seen from this browser's configured API address. */
export function readingsUrl(configured: string | undefined, origin: string) {
  const base = configured && /^https?:\/\//i.test(configured) ? configured : `${origin}/api/v1`
  return `${base.replace(/\/+$/, '')}/sensors/readings`
}

export function readingsExample(url: string) {
  return `curl -X POST '${url}' \\
  -H 'X-Api-Key: <your API key>' \\
  -H 'Content-Type: application/json' \\
  -d '{"readings":[{"deviceId":"fridge-01","value":3.4,"recordedAt":"${new Date().toISOString()}"}]}'`
}

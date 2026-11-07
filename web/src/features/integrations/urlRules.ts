/**
 * Friendly client-side mirror of the API's WebhookTargetGuard (https only, no credentials, no localhost or
 * private literals). The server stays authoritative and also checks the resolved address.
 */
export function webhookUrlProblem(value: string): string | null {
  const text = value.trim()
  if (!text) return 'Enter the URL to call.'
  let url: URL
  try {
    url = new URL(text)
  } catch {
    return 'Enter an absolute URL such as https://example.com/hooks/trail.'
  }
  if (url.protocol !== 'https:') return 'The URL must use https.'
  if (url.username || url.password) return 'The URL must not contain credentials.'
  const host = url.hostname.replace(/\.$/, '').toLowerCase()
  if (host === 'localhost' || host.endsWith('.localhost')) {
    return 'The URL must not point at localhost.'
  }
  if (isPrivateLiteral(host)) {
    return 'The URL must not point at a private, loopback or link-local address.'
  }
  return null
}

function isPrivateLiteral(host: string): boolean {
  const v4 = /^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$/.exec(host)
  if (v4) {
    const [a, b] = [Number(v4[1]), Number(v4[2])]
    return (
      a === 0 ||
      a === 10 ||
      a === 127 ||
      a >= 224 ||
      (a === 100 && b >= 64 && b <= 127) ||
      (a === 169 && b === 254) ||
      (a === 172 && b >= 16 && b <= 31) ||
      (a === 192 && b === 168)
    )
  }
  if (host.startsWith('[')) {
    const v6 = host.slice(1, -1)
    return (
      v6 === '::' ||
      v6 === '::1' ||
      /^f[cd]/.test(v6) ||
      /^fe[89ab]/.test(v6) ||
      v6.startsWith('::ffff:')
    )
  }
  return false
}

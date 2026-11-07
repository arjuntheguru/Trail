import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { ArrowLeft, Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'

import type { components } from '@/api/schema'
import { unwrap } from '@/api/unwrap'
import { useAuthService, useSession } from '@/auth/context'
import { Permissions } from '@/auth/permissions'
import { FormAlert, FormField } from '@/components/FormField'
import { Button, buttonVariants } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

import { applyServerError } from './formErrors'
import { regionsQuery, siteQuery, type Region, type Site } from './queries'
import { ListSkeleton, LoadError, RequirePermission } from './shared'
import { useSettingsMutation } from './useSettingsMutation'
import { TimeZonePicker } from './TimeZonePicker'
import { browserTimeZone } from './timeZones'

type Day = components['schemas']['DayOfWeek']

// Monday first, like the API stores them.
const DAYS: readonly Day[] = [
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
  'Sunday',
]

// Mirrors Site / Address max lengths and rules; the server stays the authority.
const text = (max: number, message: string) => z.string().trim().max(max, message)
const siteSchema = z
  .object({
    name: z.string().trim().min(1, 'Enter a name.').max(200, 'The name is too long.'),
    code: text(32, 'The code is too long.'),
    regionId: z.string(),
    timeZone: z.string().min(1, 'Choose a time zone.'),
    phone: text(32, 'The phone number is too long.'),
    email: text(254, 'The email is too long.').refine(
      (v) => v === '' || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v),
      'Enter a valid email address.',
    ),
    addressLine1: text(200, 'The address line is too long.'),
    addressLine2: text(200, 'The address line is too long.'),
    addressCity: text(100, 'The city is too long.'),
    addressState: text(100, 'The state or region is too long.'),
    addressPostalCode: text(20, 'The postal code is too long.'),
    addressCountryCode: z.string().trim(),
    hours: z.array(
      z.object({
        day: z.string(),
        open: z.boolean(),
        opens: z.string(),
        closes: z.string(),
      }),
    ),
  })
  .superRefine((v, ctx) => {
    const anyAddress = [
      v.addressLine1,
      v.addressLine2,
      v.addressCity,
      v.addressState,
      v.addressPostalCode,
      v.addressCountryCode,
    ].some((x) => x !== '')
    if (anyAddress) {
      if (!v.addressLine1)
        ctx.addIssue({
          code: 'custom',
          path: ['addressLine1'],
          message: 'Enter the first address line.',
        })
      if (!v.addressCity)
        ctx.addIssue({ code: 'custom', path: ['addressCity'], message: 'Enter the city.' })
      if (!/^[A-Za-z]{2}$/.test(v.addressCountryCode))
        ctx.addIssue({
          code: 'custom',
          path: ['addressCountryCode'],
          message: 'Use a two-letter country code such as GB.',
        })
    }
    v.hours.forEach((h, i) => {
      if (h.open && (!h.opens || !h.closes))
        ctx.addIssue({
          code: 'custom',
          path: ['hours', i, 'opens'],
          message: 'Set opening and closing times.',
        })
    })
  })
type SiteValues = z.infer<typeof siteSchema>
type SiteField = keyof SiteValues

const FIELDS: readonly SiteField[] = [
  'name',
  'code',
  'regionId',
  'timeZone',
  'phone',
  'email',
  'addressLine1',
  'addressLine2',
  'addressCity',
  'addressState',
  'addressPostalCode',
  'addressCountryCode',
]

const ALIASES: Record<string, SiteField> = {
  'Address.Line1': 'addressLine1',
  'Address.Line2': 'addressLine2',
  'Address.City': 'addressCity',
  'Address.StateOrRegion': 'addressState',
  'Address.PostalCode': 'addressPostalCode',
  'Address.CountryCode': 'addressCountryCode',
}

const CODE_FIELDS: Record<string, SiteField> = {
  'site.name_taken': 'name',
  'site.unknown_region': 'regionId',
  'site.region_out_of_scope': 'regionId',
}

const hhmm = (time: string) => time.slice(0, 5)
/** Time inputs give HH:mm; the API's TimeOnly wants seconds. */
const withSeconds = (time: string) => (time.length === 5 ? `${time}:00` : time)

function toValues(site?: Site): SiteValues {
  return {
    name: site?.name ?? '',
    code: site?.code ?? '',
    regionId: site?.regionId ?? '',
    timeZone: site?.timeZone ?? browserTimeZone(),
    phone: site?.phone ?? '',
    email: site?.email ?? '',
    addressLine1: site?.address?.line1 ?? '',
    addressLine2: site?.address?.line2 ?? '',
    addressCity: site?.address?.city ?? '',
    addressState: site?.address?.stateOrRegion ?? '',
    addressPostalCode: site?.address?.postalCode ?? '',
    addressCountryCode: site?.address?.countryCode ?? '',
    hours: DAYS.map((day) => {
      const found = site?.openingHours.find((h) => h.day === day)
      return {
        day,
        open: !!found,
        opens: found ? hhmm(found.opens) : '09:00',
        closes: found ? hhmm(found.closes) : '17:00',
      }
    }),
  }
}

function toBody(v: SiteValues): components['schemas']['CreateSiteCommand'] {
  const hasAddress = !!(v.addressLine1 || v.addressCity || v.addressCountryCode)
  const orNull = (s: string) => s || null
  return {
    name: v.name,
    code: orNull(v.code),
    regionId: orNull(v.regionId),
    timeZone: v.timeZone,
    phone: orNull(v.phone),
    email: orNull(v.email),
    address: hasAddress
      ? {
          line1: v.addressLine1,
          line2: orNull(v.addressLine2),
          city: v.addressCity,
          stateOrRegion: orNull(v.addressState),
          postalCode: orNull(v.addressPostalCode),
          countryCode: v.addressCountryCode.toUpperCase(),
        }
      : null,
    openingHours: v.hours
      .filter((h) => h.open)
      .map((h) => ({
        day: h.day as Day,
        opens: withSeconds(h.opens),
        closes: withSeconds(h.closes),
      })),
  }
}

const selectClass =
  'h-11 w-full rounded-md border border-input bg-card px-3 text-base md:h-10 md:text-sm'

export function SiteFormPage({ siteId }: { siteId?: string }) {
  return (
    <RequirePermission permission={Permissions.Sites.Manage}>
      <SiteLoader siteId={siteId} />
    </RequirePermission>
  )
}

function SiteLoader({ siteId }: { siteId?: string }) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const site = useQuery({ ...siteQuery(client, organizationId, siteId ?? ''), enabled: !!siteId })
  // The region list must be there before the select is, or an existing region would not show as selected.
  const regions = useQuery(regionsQuery(client, organizationId))

  return (
    <section className="flex flex-col gap-4">
      <Link
        to="/settings/sites"
        className="inline-flex w-fit items-center gap-1 text-sm text-muted-foreground hover:underline"
      >
        <ArrowLeft className="size-4" aria-hidden />
        Back to sites
      </Link>
      <h2 className="text-xl font-semibold">{siteId ? 'Edit site' : 'Add site'}</h2>
      {(siteId && site.isPending) || regions.isPending ? (
        <ListSkeleton rows={3} />
      ) : siteId && site.isError ? (
        <LoadError error={site.error} retry={() => void site.refetch()} />
      ) : regions.isError ? (
        <LoadError error={regions.error} retry={() => void regions.refetch()} />
      ) : (
        <SiteForm site={site.data} regions={regions.data} />
      )}
    </section>
  )
}

function SiteForm({ site, regions }: { site?: Site; regions: readonly Region[] }) {
  const { client } = useAuthService()
  const navigate = useNavigate()
  const [general, setGeneral] = useState<string[]>([])
  const form = useForm<SiteValues>({
    resolver: zodResolver(siteSchema),
    defaultValues: toValues(site),
  })
  const { errors } = form.formState
  const mutation = useSettingsMutation({
    run: (values: SiteValues) => {
      const body = toBody(values)
      return site
        ? unwrap(client.PUT('/api/v1/sites/{id}', { params: { path: { id: site.id } }, body }))
        : unwrap(client.POST('/api/v1/sites', { body }))
    },
    areas: [['sites']],
    onSuccess: () => {
      toast.success(site ? 'Site saved.' : 'Site added.')
      void navigate({ to: '/settings/sites' })
    },
  })
  const submit = form.handleSubmit((values) => {
    setGeneral([])
    mutation.mutate(values, {
      onError: (error) =>
        setGeneral(applyServerError(error, FIELDS, form.setError, CODE_FIELDS, ALIASES)),
    })
  })
  const timeZone = useWatch({ control: form.control, name: 'timeZone' })
  const hours = useWatch({ control: form.control, name: 'hours' })

  return (
    <form onSubmit={(e) => void submit(e)} noValidate className="flex flex-col gap-5">
      <FormField label="Name" autoComplete="off" error={errors.name} {...form.register('name')} />
      <FormField
        label="Code"
        hint="Optional short reference, for example LDN01."
        autoComplete="off"
        error={errors.code}
        {...form.register('code')}
      />
      <div className="flex flex-col gap-2">
        <Label htmlFor="site-region">Region</Label>
        <select id="site-region" className={selectClass} {...form.register('regionId')}>
          <option value="">No region</option>
          {regions.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
        </select>
        {errors.regionId && (
          <p role="alert" className="text-sm text-destructive">
            {errors.regionId.message}
          </p>
        )}
      </div>
      <TimeZonePicker
        value={timeZone}
        onChange={(zone) => form.setValue('timeZone', zone, { shouldDirty: true })}
        error={errors.timeZone?.message}
      />

      <fieldset className="flex flex-col gap-4">
        <legend className="mb-2 font-medium">Contact</legend>
        <FormField label="Phone" type="tel" error={errors.phone} {...form.register('phone')} />
        <FormField label="Email" type="email" error={errors.email} {...form.register('email')} />
      </fieldset>

      <fieldset className="flex flex-col gap-4">
        <legend className="mb-2 font-medium">Address</legend>
        <FormField
          label="Address line 1"
          error={errors.addressLine1}
          {...form.register('addressLine1')}
        />
        <FormField
          label="Address line 2"
          error={errors.addressLine2}
          {...form.register('addressLine2')}
        />
        <FormField label="City" error={errors.addressCity} {...form.register('addressCity')} />
        <FormField
          label="State or region"
          error={errors.addressState}
          {...form.register('addressState')}
        />
        <FormField
          label="Postal code"
          error={errors.addressPostalCode}
          {...form.register('addressPostalCode')}
        />
        <FormField
          label="Country code"
          hint="Two letters, for example GB."
          maxLength={2}
          error={errors.addressCountryCode}
          {...form.register('addressCountryCode')}
        />
      </fieldset>

      <fieldset className="flex flex-col gap-2">
        <legend className="mb-1 font-medium">Opening hours</legend>
        <p className="text-sm text-muted-foreground">
          A closing time at or before the opening time means the site closes after midnight.
        </p>
        {DAYS.map((day, i) => (
          <div key={day} className="flex flex-wrap items-center gap-3 rounded-lg border p-2">
            <label className="flex min-w-32 items-center gap-2 text-sm font-medium">
              <input
                type="checkbox"
                className="size-5 accent-primary"
                {...form.register(`hours.${i}.open`)}
              />
              {day}
            </label>
            {hours[i]?.open ? (
              <>
                <Input
                  type="time"
                  aria-label={`${day} opens`}
                  className="w-32"
                  {...form.register(`hours.${i}.opens`)}
                />
                <span aria-hidden>to</span>
                <Input
                  type="time"
                  aria-label={`${day} closes`}
                  className="w-32"
                  {...form.register(`hours.${i}.closes`)}
                />
              </>
            ) : (
              <span className="text-sm text-muted-foreground">Closed</span>
            )}
            {errors.hours?.[i]?.opens && (
              <p role="alert" className="w-full text-sm text-destructive">
                {errors.hours[i].opens.message}
              </p>
            )}
          </div>
        ))}
      </fieldset>

      <FormAlert messages={general} />
      <div className="flex gap-2">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending && <Loader2 className="animate-spin" aria-hidden />}
          {site ? 'Save site' : 'Add site'}
        </Button>
        <Link to="/settings/sites" className={buttonVariants({ variant: 'outline' })}>
          Cancel
        </Link>
      </div>
    </form>
  )
}

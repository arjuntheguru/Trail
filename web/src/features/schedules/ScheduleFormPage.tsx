import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { ArrowLeft, Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'

import { unwrap } from '@/api/unwrap'
import { useAuthService, useSession } from '@/auth/context'
import { FormAlert, FormField } from '@/components/FormField'
import { Button, buttonVariants } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { applyServerError } from '@/features/settings/formErrors'
import { ListSkeleton, LoadError, RequirePermission } from '@/features/settings/shared'
import { useAccessOptions } from '@/features/settings/useAccessOptions'
import { useSettingsMutation } from '@/features/settings/useSettingsMutation'

import {
  ALIASES,
  CODE_FIELDS,
  FIELDS,
  scheduleSchema,
  toBody,
  toRecurrence,
  toValues,
  type ScheduleValues,
} from './form'
import { SCHEDULES_MANAGE, scheduleQuery, templatesPickerQuery, type Schedule } from './queries'
import {
  FREQUENCIES,
  SINGLE_DAYS,
  WeekDay,
  daysFromList,
  daysToList,
  nextOccurrences,
  validateRecurrence,
  type Frequency,
  type SingleDay,
} from './recurrence'
import { formatOccurrence, summarizeSchedule, todayIso } from './summary'
import { TargetPicker } from './TargetPicker'

const selectClass =
  'h-11 w-full rounded-md border border-input bg-card px-3 text-base md:h-10 md:text-sm'

const FREQUENCY_LABELS: Record<Frequency, string> = {
  Once: 'Once',
  Daily: 'Daily',
  Weekly: 'Weekly',
  Monthly: 'Monthly',
  Yearly: 'Yearly',
}

const UNITS: Record<Frequency, string> = {
  Once: '',
  Daily: 'day(s)',
  Weekly: 'week(s)',
  Monthly: 'month(s)',
  Yearly: 'year(s)',
}

const MONTH_DAYS = Array.from({ length: 31 }, (_, i) => i + 1)

export function ScheduleFormPage({ scheduleId }: { scheduleId?: string }) {
  return (
    <RequirePermission permission={SCHEDULES_MANAGE}>
      <ScheduleLoader scheduleId={scheduleId} />
    </RequirePermission>
  )
}

function ScheduleLoader({ scheduleId }: { scheduleId?: string }) {
  const { client } = useAuthService()
  const organizationId = useSession().organization?.id
  const schedule = useQuery({
    ...scheduleQuery(client, organizationId, scheduleId ?? ''),
    enabled: !!scheduleId,
  })
  // Options must be there before the selects are, or an existing choice would not show as selected.
  const templates = useQuery(templatesPickerQuery(client, organizationId))
  const options = useAccessOptions()

  return (
    <section className="flex flex-col gap-4">
      <Link
        to="/settings/schedules"
        className="inline-flex w-fit items-center gap-1 text-sm text-muted-foreground hover:underline"
      >
        <ArrowLeft className="size-4" aria-hidden />
        Back to schedules
      </Link>
      <h2 className="text-xl font-semibold">{scheduleId ? 'Edit schedule' : 'Add schedule'}</h2>
      {(scheduleId && schedule.isPending) || templates.isPending || options.isPending ? (
        <ListSkeleton rows={3} />
      ) : scheduleId && schedule.isError ? (
        <LoadError error={schedule.error} retry={() => void schedule.refetch()} />
      ) : templates.isError ? (
        <LoadError error={templates.error} retry={() => void templates.refetch()} />
      ) : options.error ? (
        <LoadError error={options.error} retry={options.refetch} />
      ) : (
        <ScheduleForm
          schedule={schedule.data}
          templates={templates.data.items.map((t) => ({ id: t.id, name: t.name }))}
          options={options}
        />
      )}
    </section>
  )
}

type Options = ReturnType<typeof useAccessOptions>

function ScheduleForm({
  schedule,
  templates,
  options,
}: {
  schedule?: Schedule
  templates: { id: string; name: string }[]
  options: Options
}) {
  const { client } = useAuthService()
  const navigate = useNavigate()
  const [general, setGeneral] = useState<string[]>([])
  const form = useForm<ScheduleValues>({
    resolver: zodResolver(scheduleSchema),
    defaultValues: toValues(schedule),
  })
  const { errors } = form.formState
  const mutation = useSettingsMutation({
    run: (values: ScheduleValues) => {
      const body = toBody(values)
      return schedule
        ? unwrap(
            client.PUT('/api/v1/schedules/{id}', { params: { path: { id: schedule.id } }, body }),
          )
        : unwrap(client.POST('/api/v1/schedules', { body }))
    },
    areas: [['schedules']],
    onSuccess: () => {
      toast.success(schedule ? 'Schedule saved.' : 'Schedule added.')
      void navigate({ to: '/settings/schedules' })
    },
  })
  const submit = form.handleSubmit((values) => {
    setGeneral([])
    mutation.mutate(values, {
      onError: (error) =>
        setGeneral(applyServerError(error, FIELDS, form.setError, CODE_FIELDS, ALIASES)),
    })
  })

  const values = useWatch({ control: form.control }) as ScheduleValues
  const setValue = <K extends keyof ScheduleValues>(name: K, value: ScheduleValues[K]) =>
    form.setValue(name, value as never, { shouldDirty: true })
  const { frequency } = values

  // A template that was switched off since still shows on the schedule that uses it.
  const templateChoices =
    schedule && !templates.some((t) => t.id === schedule.templateId)
      ? [{ id: schedule.templateId, name: `${schedule.templateName} (inactive)` }, ...templates]
      : templates

  return (
    <form onSubmit={(e) => void submit(e)} noValidate className="flex flex-col gap-5">
      <div className="flex flex-col gap-2">
        <Label htmlFor="schedule-template">Template</Label>
        <select
          id="schedule-template"
          className={selectClass}
          aria-invalid={errors.templateId ? true : undefined}
          {...form.register('templateId')}
        >
          <option value="">Choose a template</option>
          {templateChoices.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>
        {errors.templateId && (
          <p role="alert" className="text-sm text-destructive">
            {errors.templateId.message}
          </p>
        )}
      </div>

      <fieldset className="flex flex-col gap-4">
        <legend className="mb-2 font-medium">When</legend>
        <div className="flex flex-col gap-2">
          <Label htmlFor="schedule-frequency">Repeats</Label>
          <select id="schedule-frequency" className={selectClass} {...form.register('frequency')}>
            {FREQUENCIES.map((f) => (
              <option key={f} value={f}>
                {FREQUENCY_LABELS[f]}
              </option>
            ))}
          </select>
        </div>

        {frequency !== 'Once' && (
          <FormField
            label={`Every how many ${UNITS[frequency]}`}
            type="number"
            inputMode="numeric"
            min={1}
            max={52}
            className="w-32"
            error={errors.interval}
            {...form.register('interval')}
          />
        )}

        {frequency === 'Weekly' && (
          <DaysPicker value={values.days} onChange={(days) => setValue('days', days)} />
        )}

        {(frequency === 'Monthly' || frequency === 'Yearly') && (
          <div className="flex flex-col gap-2">
            <Label htmlFor="schedule-day-of-month">On day</Label>
            <select
              id="schedule-day-of-month"
              className={selectClass}
              {...form.register('dayOfMonth')}
            >
              <option value="">Same day as the start date</option>
              {MONTH_DAYS.map((d) => (
                <option key={d} value={d}>
                  {d}
                </option>
              ))}
              <option value="-1">Last day of the month</option>
            </select>
            <p className="text-sm text-muted-foreground">
              In shorter months the task falls on the last day.
            </p>
          </div>
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <FormField
            label={frequency === 'Once' ? 'Date' : 'Starts on'}
            type="date"
            error={errors.startDate}
            {...form.register('startDate')}
          />
          {frequency !== 'Once' && (
            <FormField
              label="Ends on"
              type="date"
              hint="Optional. Leave empty to repeat for ever."
              error={errors.endDate}
              {...form.register('endDate')}
            />
          )}
        </div>
        {errors.recurrence && (
          <p role="alert" className="text-sm text-destructive">
            {errors.recurrence.message}
          </p>
        )}
      </fieldset>

      <fieldset className="flex flex-col gap-4">
        <legend className="mb-2 font-medium">Times</legend>
        <div className="grid gap-4 sm:grid-cols-2">
          <FormField
            label="Available from"
            type="time"
            hint="Optional. Leave empty to show from the start of the day."
            error={errors.availableFrom}
            {...form.register('availableFrom')}
          />
          <FormField
            label="Due by"
            type="time"
            error={errors.dueTime}
            {...form.register('dueTime')}
          />
        </div>
        <TimeZoneNote values={values} options={options} />
      </fieldset>

      <fieldset className="flex flex-col gap-3">
        <legend className="mb-2 font-medium">Where</legend>
        {!options.available && (
          <p className="text-sm text-muted-foreground">
            You need permission to view sites to choose where the schedule runs.
          </p>
        )}
        <TargetPicker
          value={{
            allSites: values.allSites,
            siteIds: values.siteIds,
            regionIds: values.regionIds,
          }}
          onChange={(t) => {
            setValue('allSites', t.allSites)
            setValue('siteIds', t.siteIds)
            setValue('regionIds', t.regionIds)
          }}
          sites={options.sites}
          regions={options.regions}
        />
        {errors.targets && (
          <p role="alert" className="text-sm text-destructive">
            {errors.targets.message}
          </p>
        )}
        <div className="flex flex-col gap-2">
          <Label htmlFor="schedule-department">Department</Label>
          <select
            id="schedule-department"
            className={selectClass}
            {...form.register('departmentId')}
          >
            <option value="">Everyone at the site</option>
            {options.departments.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </select>
          {errors.departmentId && (
            <p role="alert" className="text-sm text-destructive">
              {errors.departmentId.message}
            </p>
          )}
        </div>
      </fieldset>

      <Preview values={values} />

      <FormAlert messages={general} />
      <div className="flex gap-2">
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending && <Loader2 className="animate-spin" aria-hidden />}
          {schedule ? 'Save schedule' : 'Add schedule'}
        </Button>
        <Link to="/settings/schedules" className={buttonVariants({ variant: 'outline' })}>
          Cancel
        </Link>
      </div>
    </form>
  )
}

function DaysPicker({
  value,
  onChange,
}: {
  value: SingleDay[]
  onChange: (days: SingleDay[]) => void
}) {
  const set = (days: number) => onChange(daysToList(days))
  const mask = daysFromList(value)
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="mb-1 text-sm font-medium">On</legend>
      <div className="flex flex-wrap gap-x-4 gap-y-1">
        {SINGLE_DAYS.map((day) => {
          const id = `schedule-day-${day}`
          return (
            <div key={day} className="flex min-h-11 items-center gap-2 md:min-h-9">
              <Checkbox
                id={id}
                checked={value.includes(day)}
                onCheckedChange={(on) =>
                  onChange(SINGLE_DAYS.filter((d) => (d === day ? on === true : value.includes(d))))
                }
              />
              <Label htmlFor={id}>{day}</Label>
            </div>
          )
        })}
      </div>
      <div className="flex flex-wrap gap-2">
        <Button
          type="button"
          variant="outline"
          size="sm"
          aria-pressed={mask === WeekDay.Weekdays}
          onClick={() => set(WeekDay.Weekdays)}
        >
          Weekdays
        </Button>
        <Button
          type="button"
          variant="outline"
          size="sm"
          aria-pressed={mask === WeekDay.All}
          onClick={() => set(WeekDay.All)}
        >
          Every day
        </Button>
      </div>
      {value.length === 0 && (
        <p className="text-sm text-muted-foreground">
          With no days chosen, the schedule runs on the weekday of the start date.
        </p>
      )}
    </fieldset>
  )
}

/** Times are in each site's own time zone; say which zones are in play. */
function TimeZoneNote({ values, options }: { values: ScheduleValues; options: Options }) {
  const regionIds = new Set(values.regionIds)
  const chosen = values.allSites
    ? options.sites
    : options.sites.filter(
        (s) => values.siteIds.includes(s.id) || (s.regionId != null && regionIds.has(s.regionId)),
      )
  const zones = [...new Set(chosen.map((s) => s.timeZone))].sort()
  return (
    <p className="text-sm text-muted-foreground">
      Times are the local time at each site
      {zones.length > 0 ? ` (${zones.join(', ')})` : ''}.
    </p>
  )
}

function Preview({ values }: { values: ScheduleValues }) {
  const recurrence = toRecurrence(values)
  const valid = recurrence !== null && validateRecurrence(recurrence).length === 0
  const siteCount = values.allSites ? 0 : values.siteIds.length
  const regionCount = values.allSites ? 0 : values.regionIds.length
  const next = valid ? nextOccurrences(recurrence, todayIso(), 10) : []
  return (
    <section aria-label="Preview" className="flex flex-col gap-2 rounded-xl border bg-card p-3">
      <h3 className="font-medium">Summary</h3>
      {valid ? (
        <p data-testid="schedule-summary">
          {summarizeSchedule({
            recurrence,
            dueTime: values.dueTime || null,
            availableFrom: values.availableFrom || null,
            targets: { allSites: values.allSites, siteCount, regionCount },
          })}
        </p>
      ) : (
        <p className="text-sm text-muted-foreground">Complete the schedule to see a summary.</p>
      )}
      {valid && (
        <>
          <h3 className="font-medium">Next occurrences</h3>
          {next.length === 0 ? (
            <p className="text-sm text-muted-foreground">There are no upcoming dates.</p>
          ) : (
            <ol aria-label="Next occurrences" className="grid gap-1 text-sm sm:grid-cols-2">
              {next.map((d) => (
                <li key={d}>{formatOccurrence(d)}</li>
              ))}
            </ol>
          )}
        </>
      )}
    </section>
  )
}

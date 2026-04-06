# NativeCal Bug Findings

## 1) All-day date range queries have an end-date off-by-one
- **File:** `NativeCal/Services/DatabaseService.cs`
- **Lines:** 83-90, 136-145
- **Problem:** The overlap predicate treats `EndTime` as if the stored all-day end were exclusive, but `EventDialog` saves all-day events as a midnight `DateTime` on the selected end date. That means an all-day event can disappear from its final day in day-based queries, especially `GetEventsForDateAsync(...)`.
- **Suggested fix:** Standardize all-day events to a single convention. The safest fix is to store all-day events with an exclusive end boundary (`endDate.AddDays(1)`) and query them with consistent `[start, end)` overlap logic.

## 2) Search LIKE queries do not escape wildcards
- **File:** `NativeCal/Services/DatabaseService.cs`
- **Lines:** 163-172
- **Problem:** The query is parameterized, so it is not SQL-injection vulnerable, but `%` and `_` inside user input are treated as LIKE wildcards. Searching for literal text such as `100%` or `_draft` will return unintended matches.
- **Suggested fix:** Escape `\`, `%`, and `_` in the search string and add `ESCAPE '\'` to the SQL.

## 3) `GetEventAsync` can return null despite a non-nullable signature
- **File:** `NativeCal/Services/DatabaseService.cs`
- **Lines:** 96-100
- **Problem:** `FirstOrDefaultAsync()` can return `null`, but the method is declared as `Task<CalendarEvent>`. Callers may assume a real object exists and crash later.
- **Suggested fix:** Change the signature to `Task<CalendarEvent?>` and update call sites to handle the missing-event case explicitly.

## 4) Calendar deletion does not enforce the default/last-calendar invariant
- **File:** `NativeCal/Services/DatabaseService.cs`
- **Lines:** 206-209
- **Problem:** The service deletes any calendar and its events without checking whether it is the last calendar or the default calendar. The UI tries to guard against this, but the invariant really belongs in the service layer too.
- **Suggested fix:** Make deletion transactional and reject deleting the last calendar. If the deleted calendar is default, atomically promote another calendar to default before committing.

## 5) Agenda "Load more" can race and advance state without reloading
- **File:** `NativeCal/ViewModels/AgendaViewModel.cs`
- **Lines:** 109-114
- **Problem:** `LoadMore()` increments `DaysToLoad` before awaiting `LoadAgenda()`. If another load is already running, `LoadAgenda()` returns immediately because `IsLoading` is true, leaving `DaysToLoad` changed but the UI not refreshed.
- **Suggested fix:** Guard `LoadMore()` itself, or queue the increment until a load can actually run. Disabling the command while loading also helps.

## 6) Agenda view drops overlapping timed events that start before the load window
- **File:** `NativeCal/ViewModels/AgendaViewModel.cs`
- **Lines:** 43-72
- **Problem:** Timed events are grouped only when `evt.StartTime.Date >= startDate`. That means an event that starts before today but overlaps today (for example, 11 PM yesterday to 1 AM today) is fetched by SQL and then thrown away by the ViewModel.
- **Suggested fix:** Group timed events by every day they overlap, not just by their start day. Reuse the same per-day overlap logic used in the week view.

## 7) Month view only buckets timed events by start date
- **File:** `NativeCal/ViewModels/MonthViewModel.cs`
- **Lines:** 66-84
- **Problem:** Non-all-day events are keyed only by `evt.StartTime.Date`. A timed event that starts before the visible month grid, or one that spans multiple days, will either be missing or appear only on its start day.
- **Suggested fix:** For month cells, distribute timed events across every visible day they overlap, or apply a per-cell overlap check before adding them.

## 8) Settings ViewModel loses the Saturday first-day-of-week option
- **File:** `NativeCal/ViewModels/SettingsViewModel.cs`
- **Lines:** 69-74
- **Problem:** The ViewModel clamps `FirstDayOfWeek` to `0..1`, but the UI/database also support `6` for Saturday. If Saturday is saved, this ViewModel will load it as Monday instead of preserving the choice.
- **Suggested fix:** Store the actual enum/value set consistently. Either use `DayOfWeek` directly or map all valid values (`0`, `1`, `6`) instead of clamping to two options.

## 9) Event dialog saves all-day end dates as midnight dates
- **File:** `NativeCal/Views/EventDialog.cs`
- **Lines:** 527-535
- **Problem:** For all-day events, `resultEnd` is saved as `allDayEndDatePicker.Date.DateTime.Date`, which is midnight at the start of the selected end date. That representation conflicts with the rest of the app’s inclusive all-day logic and contributes to the end-date off-by-one issue above.
- **Suggested fix:** Normalize all-day events to a single convention. Prefer storing an exclusive end boundary (`selectedEndDate.AddDays(1)`) and update display/query code accordingly.

## 10) Event dialog can crash or save an invalid calendar when no default calendar exists
- **File:** `NativeCal/Views/EventDialog.cs`
- **Lines:** 548-556
- **Problem:** If the selected index is invalid, the code falls back to `calendars.First(c => c.IsDefault)`. That throws when there is no default calendar. If there are zero calendars, `calendarId` stays `0`, which is also invalid.
- **Suggested fix:** Disable Save until a valid calendar exists, and use `FirstOrDefault()` with an explicit fallback/error message instead of `First(...)`.

## 11) Recurrence is captured but never actually expanded
- **File:** `NativeCal/Views/EventDialog.cs`
- **Lines:** 356-380, 564-580
- **Problem:** The recurrence picker only serializes an enum name into `RecurrenceRule`. There is no corresponding recurrence expansion in `DatabaseService` or the ViewModels, so recurring events still behave like one-off events.
- **Suggested fix:** Either remove the recurrence UI until recurrence is implemented, or add recurrence expansion when fetching events for day/week/month/agenda views.

## 12) Settings page loads are fire-and-forget and can overwrite settings during initialization
- **File:** `NativeCal/Views/SettingsPage.xaml.cs`
- **Lines:** 51-55, 59-125, 130-183
- **Problem:** `LoadData()` starts `LoadSettingsAsync()` and `LoadCalendarListAsync()` without awaiting them. Because the combo boxes already have default `SelectedIndex` values in XAML and the selection-changed handlers are active, startup can write default values back to the database before saved values are loaded. Any exception in those tasks is also unobserved.
- **Suggested fix:** Make `LoadData()` await the work (`Task.WhenAll(...)`), or set a loading guard before controls can raise `SelectionChanged`. Removing the XAML default indices also reduces the race.

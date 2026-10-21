using Trail.Domain.Tasks;

namespace Trail.Application.Features.Templates;

public sealed record LibraryTemplate(
    string Key,
    string Name,
    string Category,
    string Description,
    TaskKind Kind,
    bool RequiresApproval,
    bool RequiresPhoto,
    IReadOnlyList<TemplateField> Fields);

/// <summary>
/// Built-in, best-practice templates an organization can copy and adapt. Ranges follow common UK food
/// safety guidance (FSA Safer Food Better Business); organizations should check them against their own HACCP plan.
/// </summary>
public static class TemplateLibrary
{
    private static TemplateField Check(string key, string label, string? section = null) =>
        new() { Key = key, Label = label, Type = FieldType.YesNo, ExpectedAnswer = true, Section = section };

    private static TemplateField Temp(string key, string label, decimal? min, decimal? max, string? section = null) =>
        new() { Key = key, Label = label, Type = FieldType.Temperature, Min = min, Max = max, Unit = "°C", Section = section, PhotoRequiredOnFailure = true };

    private static TemplateField Text(string key, string label, bool required = true) =>
        new() { Key = key, Label = label, Type = FieldType.Text, Required = required, RaiseIssueOnFailure = false };

    private static TemplateField Long(string key, string label, bool required = true) =>
        new() { Key = key, Label = label, Type = FieldType.LongText, Required = required, RaiseIssueOnFailure = false };

    public static IReadOnlyList<LibraryTemplate> All { get; } =
    [
        new("opening-checks", "Opening checks", "Daily operations",
            "Start-of-day safety and readiness checks before the doors open.", TaskKind.Checklist, false, false,
            [
                Check("staff_fit", "All staff on shift are fit for work (no sickness or diarrhoea in the last 48 hours)", "Team"),
                Check("handwash", "Hand-wash basins stocked with soap, hot water and paper towels", "Hygiene"),
                Check("surfaces", "Food preparation surfaces cleaned and sanitised", "Hygiene"),
                Check("pests", "No signs of pests", "Hygiene"),
                Temp("fridge_open", "Fridge temperature", null, 5, "Temperatures"),
                Temp("freezer_open", "Freezer temperature", null, -18, "Temperatures"),
                Check("probe", "Probe thermometer working and sanitised", "Equipment"),
                Check("fire_exits", "Fire exits clear and unlocked", "Safety"),
            ]),
        new("closing-checks", "Closing checks", "Daily operations",
            "End-of-day checks that the site is clean, safe and secure.", TaskKind.Checklist, true, false,
            [
                Check("food_covered", "Food covered, labelled and stored correctly", "Food"),
                Check("waste", "Out-of-date and waste food removed", "Food"),
                Check("clean_down", "Kitchen and equipment cleaned down", "Hygiene"),
                Temp("fridge_close", "Fridge temperature", null, 5, "Temperatures"),
                Check("bins", "Bins emptied and lids closed", "Hygiene"),
                Check("equipment_off", "Cooking equipment and gas turned off", "Safety"),
                Check("secure", "Doors and windows locked, alarm set", "Security"),
                new() { Key = "cash_up", Label = "Till cashed up and float counted", Type = FieldType.Checkbox, Section = "Cash" },
            ]),
        new("fridge-temperature-log", "Fridge and freezer temperature log", "Food safety",
            "Record cold storage temperatures. Readings above 5°C (fridge) or -18°C (freezer) need corrective action.",
            TaskKind.RecordLog, false, false,
            [
                new()
                {
                    Key = "unit", Label = "Unit", Type = FieldType.SingleSelect, RaiseIssueOnFailure = false,
                    Options = [new("fridge_1", "Fridge 1"), new("fridge_2", "Fridge 2"), new("freezer_1", "Freezer 1"), new("walk_in", "Walk-in cold room")],
                },
                Temp("reading", "Temperature", -25, 5),
            ]),
        new("hot-holding-log", "Hot holding temperature log", "Food safety",
            "Hot food on display must be kept at 63°C or above.", TaskKind.RecordLog, false, false,
            [
                Text("dish", "Dish"),
                Temp("reading", "Core temperature", 63, null),
            ]),
        new("cooking-temperature-log", "Cooking and reheating log", "Food safety",
            "Probe the thickest part: cooked or reheated food must reach 75°C (or a validated time/temperature).",
            TaskKind.RecordLog, false, false,
            [
                Text("dish", "Dish"),
                new()
                {
                    Key = "stage", Label = "Stage", Type = FieldType.SingleSelect, RaiseIssueOnFailure = false,
                    Options = [new("cooked", "Cooked"), new("reheated", "Reheated")],
                },
                Temp("reading", "Core temperature", 75, null),
            ]),
        new("delivery-check", "Delivery check", "Food safety",
            "Check every delivery before accepting it.", TaskKind.Form, false, true,
            [
                Text("supplier", "Supplier"),
                Temp("chilled", "Chilled goods temperature", null, 8),
                Temp("frozen", "Frozen goods temperature", null, -15),
                new()
                {
                    Key = "packaging", Label = "Packaging condition", Type = FieldType.SingleSelect,
                    Options = [new("intact", "Intact"), new("damaged", "Damaged", IsFailure: true)],
                },
                Check("dates", "Use-by dates acceptable"),
                new() { Key = "accepted", Label = "Delivery accepted", Type = FieldType.YesNo, ExpectedAnswer = true },
                new() { Key = "signature", Label = "Received by", Type = FieldType.Signature },
            ]),
        new("cleaning-schedule", "Daily cleaning schedule", "Hygiene",
            "Routine cleaning tasks for the kitchen and front of house.", TaskKind.Checklist, false, false,
            [
                new() { Key = "floors", Label = "Floors swept and mopped", Type = FieldType.Checkbox },
                new() { Key = "fryers", Label = "Fryers filtered", Type = FieldType.Checkbox },
                new() { Key = "extract", Label = "Extraction canopy wiped", Type = FieldType.Checkbox },
                new() { Key = "toilets", Label = "Customer toilets cleaned", Type = FieldType.Checkbox },
                new() { Key = "chemicals", Label = "Cleaning chemicals stored safely", Type = FieldType.Checkbox },
            ]),
        new("allergen-check", "Allergen information check", "Food safety",
            "Confirm allergen information is accurate for today's menu (UK 14 allergens).", TaskKind.Checklist, false, false,
            [
                Check("matrix", "Allergen matrix matches today's dishes and specials"),
                Check("labels", "Pre-packed food labelled with allergens (PPDS)"),
                Check("briefed", "Team briefed on today's allergens"),
            ]),
        new("team-health-check", "Team health check", "Team",
            "Fitness-to-work declaration at the start of each shift.", TaskKind.Form, false, false,
            [
                Text("name", "Team member"),
                Check("symptoms", "Free from vomiting, diarrhoea, fever and infected wounds in the last 48 hours"),
                new() { Key = "signature", Label = "Signature", Type = FieldType.Signature },
            ]),
        new("fire-alarm-test", "Weekly fire alarm test", "Health and safety",
            "Test a different call point each week and record the result.", TaskKind.Form, false, false,
            [
                Text("call_point", "Call point tested"),
                Check("sounded", "Alarm sounded throughout the premises"),
                Check("reset", "System reset correctly"),
                Long("notes", "Notes", required: false),
            ]),
        new("pest-control-check", "Pest control check", "Hygiene",
            "Weekly inspection for signs of pests.", TaskKind.Checklist, false, false,
            [
                Check("droppings", "No droppings, gnawing or nests found"),
                Check("proofing", "Doors, windows and screens pest-proofed"),
                Check("bait", "Bait stations intact (if fitted)"),
            ]),
        new("incident-report", "Incident report", "Health and safety",
            "Record an accident, near miss or customer complaint and the action taken.", TaskKind.Form, true, false,
            [
                new()
                {
                    Key = "type", Label = "Type of incident", Type = FieldType.SingleSelect, RaiseIssueOnFailure = false,
                    Options = [new("accident", "Accident or injury"), new("near_miss", "Near miss"), new("complaint", "Customer complaint"), new("food", "Food safety incident"), new("other", "Other")],
                },
                new() { Key = "when", Label = "Time of incident", Type = FieldType.Time },
                Text("people", "People involved"),
                Long("description", "What happened"),
                Long("action", "Action taken"),
                new() { Key = "photos", Label = "Photos", Type = FieldType.Photo, Required = false },
                new() { Key = "riddor", Label = "Reportable under RIDDOR?", Type = FieldType.YesNo, ExpectedAnswer = false },
            ]),
        new("maintenance-log", "Maintenance log", "Health and safety",
            "Report faulty equipment or premises issues.", TaskKind.Form, false, false,
            [
                Text("equipment", "Equipment or area"),
                Long("fault", "Fault description"),
                new()
                {
                    Key = "priority", Label = "Priority", Type = FieldType.SingleSelect,
                    Options = [new("low", "Low"), new("medium", "Medium"), new("high", "High - affects food safety", IsFailure: true)],
                },
                new() { Key = "photos", Label = "Photos", Type = FieldType.Photo, Required = false },
            ]),
        new("coffee-brew-log", "Coffee brew log", "Daily operations",
            "Dial in the espresso and record the recipe.", TaskKind.RecordLog, false, false,
            [
                new() { Key = "dose", Label = "Dose (g)", Type = FieldType.Number, Min = 16, Max = 22, Unit = "g", CommentRequiredOnFailure = false, RaiseIssueOnFailure = false },
                new() { Key = "yield", Label = "Yield (g)", Type = FieldType.Number, Min = 30, Max = 50, Unit = "g", CommentRequiredOnFailure = false, RaiseIssueOnFailure = false },
                new() { Key = "time", Label = "Extraction time (s)", Type = FieldType.Number, Min = 25, Max = 35, Unit = "s", CommentRequiredOnFailure = false, RaiseIssueOnFailure = false },
            ]),
    ];

    public static LibraryTemplate? Find(string key) => All.FirstOrDefault(t => t.Key == key);
}

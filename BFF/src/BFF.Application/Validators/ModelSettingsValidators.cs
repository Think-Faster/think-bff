using BFF.Contracts.ModelSettings;
using BFF.Models.Enums;
using System.Globalization;
using FluentValidation;

namespace BFF.Application.Validators;

public sealed class CreateModelVersionRequestValidator : AbstractValidator<CreateModelVersionRequest>
{
    public CreateModelVersionRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateCoefficientRequestValidator : AbstractValidator<CreateCoefficientRequest>
{
    public CreateCoefficientRequestValidator()
    {
        RuleFor(x => x.Share).InclusiveBetween(0, 1);
        RuleFor(x => x.RejectK).InclusiveBetween(0, 1).When(x => x.RejectK.HasValue);
    }
}

public sealed class CreateRetrainJobRequestValidator : AbstractValidator<CreateRetrainJobRequest>
{
    public CreateRetrainJobRequestValidator()
    {
        RuleFor(x => x.ParamsJson).MaximumLength(10_000);
    }
}

public sealed class CreateIgnoredRangeRequestValidator : AbstractValidator<CreateIgnoredRangeRequest>
{
    public CreateIgnoredRangeRequestValidator()
    {
        RuleFor(x => x.DateTo).GreaterThanOrEqualTo(x => x.DateFrom);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ObjectId).NotNull().When(x => x.Scope == IgnoredRangeScope.Object)
            .WithMessage("objectId is required when scope is 'object'.");
        RuleFor(x => x.SensorId).NotNull().When(x => x.Scope == IgnoredRangeScope.Sensor)
            .WithMessage("sensorId is required when scope is 'sensor'.");
    }
}

public sealed class UpsertWorkScheduleEntryRequestValidator : AbstractValidator<UpsertWorkScheduleEntryRequest>
{
    public UpsertWorkScheduleEntryRequestValidator()
    {
        RuleFor(x => x.WorkKind).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt);
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.RemovedSensor).MaximumLength(100);
        RuleFor(x => x.Comment).MaximumLength(1000);
        RuleForEach(x => x.IncidentTypes).NotEmpty().MaximumLength(100);
    }
}

public sealed class SwitchModelVersionRequestValidator : AbstractValidator<SwitchModelVersionRequest>
{
    public SwitchModelVersionRequestValidator()
    {
        RuleFor(x => x.Type).Must(t => PredictionTypeExtensions.ForecastModelNames.Contains(t))
            .WithMessage("type must be one of: " + string.Join(", ", PredictionTypeExtensions.ForecastModelNames) + ".");
        RuleFor(x => x.VersionId).GreaterThan(0).When(x => x.VersionId.HasValue);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

/// <summary>Границы долей — в схеме модели (/status settings_bounds), здесь только форма снимка: модель
/// проверит границы сама и отбросит неверный снимок с записью в аудит.</summary>
public sealed class OperatingSettingsRequestValidator : AbstractValidator<OperatingSettingsRequest>
{
    public OperatingSettingsRequestValidator()
    {
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Types)
            .Must(t => t.Count == PredictionTypeExtensions.ForecastModelNames.Count
                       && PredictionTypeExtensions.ForecastModelNames.All(t.ContainsKey))
            .WithMessage("types must contain exactly: " + string.Join(", ", PredictionTypeExtensions.ForecastModelNames) + ".");
        RuleForEach(x => x.Types).ChildRules(entry =>
        {
            entry.RuleFor(e => e.Value.Share).ExclusiveBetween(0, 1);
            entry.RuleFor(e => e.Value.RejectK).InclusiveBetween(0, 0.5).When(e => e.Value.RejectK.HasValue);
        });
    }
}

public sealed class IgnoredPeriodsRequestValidator : AbstractValidator<IgnoredPeriodsRequest>
{
    public const string TimeFormat = "yyyy-MM-dd HH:mm";

    public IgnoredPeriodsRequestValidator()
    {
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleForEach(x => x.Rows).ChildRules(row =>
        {
            row.RuleFor(r => r.A).Must(BeTime).WithMessage("a must be '" + TimeFormat + "' (Moscow time).");
            row.RuleFor(r => r.B).Must(BeTime).WithMessage("b must be '" + TimeFormat + "' (Moscow time).");
            row.RuleFor(r => r).Must(r => !BeTime(r.A) || !BeTime(r.B) || Parse(r.A) < Parse(r.B))
                .WithMessage("a must be earlier than b.");
            row.RuleFor(r => r.Comment).MaximumLength(500);
        });
    }

    private static bool BeTime(string value) =>
        DateTime.TryParseExact(value, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static DateTime Parse(string value) =>
        DateTime.ParseExact(value, TimeFormat, CultureInfo.InvariantCulture);
}

namespace BFF.Models.Enums;

public enum PredictionStatus
{
    New = 1,
    InReview = 2,
    Taken = 3,
    Rejected = 4,
    Muted = 5,
    Closed = 6,

    /// <summary>Тревога модели кончилась (alarm=false), решения по карточке не было (ML/INTEGRATION.md §9.8).</summary>
    Expired = 7,
}

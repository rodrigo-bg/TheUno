namespace TheUno.Core.Models;

public record Card(Guid Id, CardColor Color, CardType Type, int? Number = null)
{
    // Facilita mostrar la carta en logs y botones mientras no hay imágenes
    public string DisplayText => Type == CardType.Number 
        ? $"{Color} {Number}" 
        : $"{Color} {Type}";
}
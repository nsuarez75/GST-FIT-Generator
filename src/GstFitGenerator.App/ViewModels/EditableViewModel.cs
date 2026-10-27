using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GstFitGenerator.App.ViewModels;

// Base de los modelos de vista que editan el proyecto: cada cambio pasa por la sesión para poder deshacerlo.
public abstract class EditableViewModel(Session session) : ObservableObject
{
    protected Session Session { get; } = session;

    // Cambia un dato del modelo si es distinto: foto para deshacer, cambio y aviso a la vista.
    protected bool Edit<T>(T current, T value, Action<T> set, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
        {
            return false;
        }
        Session.BeforeChange(GetHashCode() + ":" + property);
        set(value);
        OnPropertyChanged(property);
        Session.AfterChange();
        return true;
    }

    // Texto de un campo numérico: vacío o un número entero; cualquier otra cosa se rechaza.
    protected static bool TryNumber(string? text, out long? value)
    {
        value = null;
        var clean = text?.Trim() ?? "";
        if (clean.Length == 0)
        {
            return true;
        }
        if (long.TryParse(clean, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n))
        {
            value = n;
            return true;
        }
        return false;
    }

    protected static string Text(long? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
}

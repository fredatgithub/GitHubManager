using System;
using System.Globalization;
using System.Windows.Data;
using GitHubManager;

namespace GitHubManager.Converters
{
    public class CloneButtonEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RepositoryLocalState state)
            {
                // Le bouton de clonage est désactivé uniquement si le dépôt est déjà cloné
                return state != RepositoryLocalState.UpToDate && state != RepositoryLocalState.NeedsUpdate;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

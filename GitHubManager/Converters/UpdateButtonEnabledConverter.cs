using System;
using System.Globalization;
using System.Windows.Data;
using GitHubManager;

namespace GitHubManager.Converters
{
    public class UpdateButtonEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RepositoryLocalState state)
            {
                // Le bouton de mise à jour est activé uniquement si le dépôt est cloné et nécessite une mise à jour
                return state == RepositoryLocalState.NeedsUpdate;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

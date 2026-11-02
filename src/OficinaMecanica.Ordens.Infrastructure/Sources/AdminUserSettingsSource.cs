using Microsoft.Extensions.Options;
using OficinaMecanica.Ordens.Infrastructure.Settings;
using OficinaMecanica.Ordens.InterfaceAdapters.DataSources;

namespace OficinaMecanica.Ordens.Infrastructure.Sources;

public class AdminUserSettingsSource : IAdminUserSource
{
    private readonly AdminUserSettings _adminUserSettings;

    public AdminUserSettingsSource(IOptions<AdminUserSettings> adminUserSettings)
    {
        _adminUserSettings = adminUserSettings.Value;
    }

    public bool CredenciaisValidas(string username, string password)
    {
        return username == _adminUserSettings.Username && password == _adminUserSettings.Password;
    }
}

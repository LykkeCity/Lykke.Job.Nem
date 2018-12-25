using JetBrains.Annotations;
using Lykke.Sdk.Settings;

namespace Lykke.Service.Nem.Settings
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class AppSettings : BaseAppSettings
    {
        public NemJobSettings NemJob { get; set; }
    }
}

using System;
using JetBrains.Annotations;
using Lykke.Common.Log;
using Lykke.Job.Nem.Services;
using Lykke.Sdk;
using Lykke.Service.BlockchainApi.Sdk;
using Lykke.Service.Nem.Settings;
using Lykke.SettingsReader;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Lykke.Service.Nem
{
    [UsedImplicitly]
    public class Startup
    {
        private readonly LykkeSwaggerOptions _swaggerOptions = new LykkeSwaggerOptions
        {
            ApiTitle = "Nem Job",
            ApiVersion = "v1"
        };

        [UsedImplicitly]
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            return services.BuildServiceProvider<AppSettings>(options =>
            {
                options.SwaggerOptions = _swaggerOptions;

                options.Logs = logs =>
                {
                    logs.AzureTableName = "NemJobLog";
                    logs.AzureTableConnectionStringResolver = settings => settings.NemJob.Db.LogsConnString;
                };
                
                options.Extend = (sc, settings) =>
                {
                    sc.AddBlockchainJob(
                        settings.ConnectionString(s => s.NemJob.Db.DataConnString),
                        settings.CurrentValue.NemJob.Period,
                        sp => new NemJob(
                            settings.CurrentValue.NemJob.NemUrl,
                            settings.CurrentValue.NemJob.HotWalletAddress,
                            settings.CurrentValue.NemJob.RequiredConfirmations,
                            sp.GetRequiredService<ILogFactory>()),
                        settings.CurrentValue.NemJob.ChaosKitty);
                };
            });
        }

        [UsedImplicitly]
        public void Configure(IApplicationBuilder app)
        {
            app.UseLykkeConfiguration(options =>
            {
                options.SwaggerOptions = _swaggerOptions;
            });
        }
    }
}

using Exiled.API.Features;
using Exiled.API.Interfaces;
using Exiled.CustomItems.API;
using Exiled.CustomItems.API.Features;
using System.Collections.Generic;

namespace PsychoFlash
{
    public class Plugin : Plugin<Config>
    {
        public override string Name => "PsychoFlash";
        public override string Prefix => "PsychoFlash";
        public override string Author => "Valeriusss";

        private Grenade psychoGrenade;

        public override void OnEnabled()
        {
            // Регистрируем кастомную гранату
            psychoGrenade = new Grenade();
            psychoGrenade.Register();

            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            // Отменяем регистрацию кастомной гранаты
            psychoGrenade?.Unregister();

            base.OnDisabled();
        }
    }
}

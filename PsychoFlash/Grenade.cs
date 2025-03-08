using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.CustomItems.API.Features;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using UnityEngine;
using MapEditorReborn.API.Features;
using MapEditorReborn.API.Features.Objects;
using Exiled.API.Enums;
using PlayerRoles;
using Exiled.API.Features.Spawn;
using CustomPlayerEffects;
using MEC;
using Exiled.API.Features.Pickups;

namespace PsychoFlash
{
    public class Grenade : CustomGrenade
    {
        public override string Name { get; set; } = "<b><color=#F5F5F5>Психоактивна граната</color></b>";
        public override string Description { get; set; } = "<b><color=#F5F5F5>Граната, що викликає психоз і галюцинації,</color> <color=#FA302D>зводячи гравців з розуму</color></b>";
        public override ItemType Type { get; set; } = ItemType.GrenadeFlash;
        public override float FuseTime { get; set; } = 5;
        public override float Weight { get; set; } = 1f;
        public override uint Id { get; set; } = 180;
        public override bool ExplodeOnCollision { get; set; } = false;
        public override SpawnProperties SpawnProperties { get; set; } = new SpawnProperties()
        {
            DynamicSpawnPoints = new List<DynamicSpawnPoint>()
            {
                new DynamicSpawnPoint()
                {
                    Location = SpawnLocationType.InsideGateB,
                    Chance = 100
                },
                new DynamicSpawnPoint()
                {
                    Location = SpawnLocationType.Inside914,
                    Chance = 100
                }
            }
        };

        private readonly Dictionary<GameObject, SchematicObject> grenadeSchematics = new Dictionary<GameObject, SchematicObject>();
        private readonly HashSet<Player> grenadeCardiacPlayers = new HashSet<Player>();

        protected override void SubscribeEvents()
        {
            Exiled.Events.Handlers.Server.RoundStarted += OnRoundStart;
            Exiled.Events.Handlers.Map.ExplodingGrenade += OnGrenadeExplode;
            Exiled.Events.Handlers.Player.ThrownProjectile += OnThrownProjectile;
            Exiled.Events.Handlers.Player.Hurting += OnPlayerHurting;
            Exiled.Events.Handlers.Player.PickingUpItem += OnPickingUpItem;
            Exiled.Events.Handlers.Player.DroppingItem += OnDroppingItem; // Новое событие!

            base.SubscribeEvents();
        }

        protected override void UnsubscribeEvents()
        {
            Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStart;
            Exiled.Events.Handlers.Map.ExplodingGrenade -= OnGrenadeExplode;
            Exiled.Events.Handlers.Player.ThrownProjectile -= OnThrownProjectile;
            Exiled.Events.Handlers.Player.Hurting -= OnPlayerHurting;
            Exiled.Events.Handlers.Player.PickingUpItem -= OnPickingUpItem;
            Exiled.Events.Handlers.Player.DroppingItem -= OnDroppingItem;

            base.UnsubscribeEvents();
        }

        // Событие броска гранаты
        private void OnThrownProjectile(ThrownProjectileEventArgs ev)
        {
            if (Check(ev.Projectile))
            {
                Log.Info($"Игрок {ev.Player.Nickname} бросил гранату на позиции {ev.Projectile.Transform.position}.");
                AttachSchematic(ev.Projectile.GameObject);
            }
        }

        // Событие, когда игрок выбрасывает гранату из инвентаря
        private void OnDroppingItem(DroppingItemEventArgs ev)
        {
            if (!Check(ev.Item))
            {
                return;
            }

            Log.Info($"Игрок {ev.Player.Nickname} выбросил гранату. Привязываем схематик.");

            Timing.CallDelayed(0.1f, () =>
            {

                var pickup = Pickup.List
                    .Where(p => p.Type == ItemType.GrenadeFlash)
                    .OrderBy(p => Vector3.Distance(p.Position, ev.Player.Position))
                    .FirstOrDefault();

                if (pickup != null)
                {
                    AttachSchematic(pickup.GameObject);
                }
                else
                {
                    Log.Warn("Pickup гранаты не найден! Возможно, он удалён или подобран.");
                }
            });
        }

        private void OnRoundStart()
        {
            Timing.CallDelayed(0.1f, () =>
            {
                foreach (var pickup in Pickup.List.Where(p => p.Type == ItemType.GrenadeFlash))
                {
                    if (Check(pickup))
                    {
                        AttachSchematic(pickup.GameObject);
                    }
                }
                Log.Info("Схематики привязаны к гранатам на карте.");
            });
        }

        // Событие взрыва гранаты
        private void OnGrenadeExplode(ExplodingGrenadeEventArgs ev)
        {
            if (!Check(ev.Projectile)) return;

            RemoveSchematic(ev.Projectile.GameObject);

            // Отмена стандартного эффекта ослепления
            ev.IsAllowed = false;

            // Наложение кастомных эффектов на игроков в радиусе
            var explosionPosition = ev.Projectile.Transform.position;
            foreach (var player in Player.List.Where(p => p.IsAlive && Vector3.Distance(p.Position, explosionPosition) <= 7f))
            {
                player.EnableEffect(EffectType.AmnesiaVision, duration: 10);
                player.EnableEffect(EffectType.Deafened, duration: 10);
                player.EnableEffect(EffectType.FogControl, intensity: 5, duration: 10);
                player.EnableEffect(EffectType.Concussed, duration: 10);
                player.EnableEffect(EffectType.CardiacArrest, duration: 10);

                grenadeCardiacPlayers.Add(player);

                Timing.CallDelayed(10.5f, () =>
                {
                    grenadeCardiacPlayers.Remove(player);
                    Log.Info($"Игрок {player.Nickname} удалён из списка защиты от урона CardiacArrest.");
                });

                Log.Info($"Эффекты применены к {player.Nickname}");
            }
        }


        // Событие урона
        private void OnPlayerHurting(HurtingEventArgs ev)
        {
            if (ev.DamageHandler.Type == DamageType.CardiacArrest && grenadeCardiacPlayers.Contains(ev.Player))
            {
                ev.IsAllowed = false;
                Log.Info($"Урон от CardiacArrest отменён для {ev.Player.Nickname}, так как эффект был выдан гранатой.");
            }
        }

        // Событие поднятия гранаты
        private void OnPickingUpItem(PickingUpItemEventArgs ev)
        {
            if (ev.Pickup != null && grenadeSchematics.ContainsKey(ev.Pickup.GameObject))
            {
                RemoveSchematic(ev.Pickup.GameObject);
                Log.Info($"Схематик удалён, так как граната была поднята игроком {ev.Player.Nickname}.");
            }
        }

        // Метод прикрепления схематика к гранате
        private void AttachSchematic(GameObject grenade)
        {
            if (grenadeSchematics.ContainsKey(grenade))
                return;

            var schematic = ObjectSpawner.SpawnSchematic("psixgranade", grenade.transform.position, Quaternion.identity, new Vector3(0.5f, 0.5f, 0.5f), null, false);
            grenadeSchematics[grenade] = schematic;

            var follow = schematic.gameObject.AddComponent<FollowGrenadeComponent>();
            follow.TargetGrenade = grenade;

            Log.Info("Схематик успешно прикреплён к гранате.");
        }

        // Метод удаления схематика
        private void RemoveSchematic(GameObject grenade)
        {
            if (grenadeSchematics.TryGetValue(grenade, out var schematic))
            {
                Object.Destroy(schematic.gameObject);
                grenadeSchematics.Remove(grenade);
                Log.Info("Схематик удалён.");
            }
        }

        // Компонент для слежения за гранатой
        public class FollowGrenadeComponent : MonoBehaviour
        {
            public GameObject TargetGrenade;

            private void Update()
            {
                if (TargetGrenade == null)
                {
                    Destroy(gameObject);
                    return;
                }

                transform.position = TargetGrenade.transform.position;
                transform.rotation = TargetGrenade.transform.rotation;
            }
        }
    }
}

﻿/**
* Copyright (c) 2017-present, PFW Contributors.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in
* compliance with the License. You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed under the License is
* distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See
* the License for the specific language governing permissions and limitations under the License.
*/

using System;

using static PFW.Units.Component.Weapon.WeaponData;

namespace PFW.Units.Component.Damage
{
    /// <summary>
    /// A collection of data structures to be used in damage calculations
    /// </summary>
    [Serializable]
    public class DamageData
    {
        /// <summary>
        /// The data structure for Reactive Explosion Armor
        /// </summary>
        public struct Era
        {
            public float Value;
            public float KEFractionMultiplier;
            public float HeatFractionMultiplier;
        }

        public struct Target
        {
            public float Armor;
            public DamageData.Era EraData;
            public float Health;
        }

        public struct KineticData
        {
            public KineticData(WeaponDamage d) {
                Power = d.Power;
                Degradation = d.ArmorDegradation;
                HealthDamageFactor = d.HealthDamageFactor;
                Friction = d.AirFriction;
            }

            /// <summary>
            /// The power of the shot
            /// </summary>
            public float Power;
            /// <summary>
            /// Multiplier for armor degradation
            /// </summary>
            public float Degradation;
            /// <summary>
            /// Multiplier for health damage
            /// </summary>
            public float HealthDamageFactor;
            /// <summary>
            /// Air friction constant used in calculation of attenuation
            /// </summary>
            public float Friction;
        }

        public struct HeatData
        {
            public HeatData(WeaponDamage d)
            {
                Power = d.Power;
                Degradation = d.ArmorDegradation;
                HealthDamageFactor = d.HealthDamageFactor;
            }

            /// <summary>
            /// The power of the shot
            /// </summary>
            public float Power;
            /// <summary>
            /// Multiplier for armor degradation
            /// </summary>
            public float Degradation;
            /// <summary>
            /// Multiplier for health damage
            /// </summary>
            public float HealthDamageFactor;
        }

        public struct FireData
        {
            public FireData(WeaponDamage d)
            {
                Power = d.Power;
                Degradation = d.ArmorDegradation;
                HealthDamageFactor = d.HealthDamageFactor;
                SuffocationDamage = d.SuffocationDamage;
            }

            /// <summary>
            /// Power of the burning effect
            /// </summary>
            public float Power;
            /// <summary>
            /// Multiplier for health damage
            /// </summary>
            public float HealthDamageFactor;
            /// <summary>
            /// Multiplier for armor degradation
            /// </summary>
            public float Degradation;
            /// <summary>
            /// A minimum damage dealt to vehicles in the fire
            /// Represents the damge to the crew by suffocation and heat
            /// </summary>
            public float SuffocationDamage;
        }

        public struct HEData
        {
            public HEData(WeaponDamage d)
            {
                Power = d.Power;
                EffectiveRadius = d.EffectiveRadius;
                HealthDamageFactor = d.HealthDamageFactor;
            }

            /// <summary>
            /// The power of the shot
            /// </summary>
            public float Power;
            /// <summary>
            /// The radius of the explosion
            /// Beyond the effective radius, the value {Remaining damage}/{Initial damage} is less than CUTOF_FFRACTION
            /// </summary>
            public float EffectiveRadius;
            /// <summary>
            /// Multiplier for health damage
            /// </summary>
            public float HealthDamageFactor;
        }

        public struct SmallArmsData
        {
            public SmallArmsData(WeaponDamage d)
            {
                Power = d.Power;
                HealthDamageFactor = d.HealthDamageFactor;
            }

            /// <summary>
            /// The power of the shot
            /// </summary>
            public float Power;
            /// <summary>
            /// Multiplier for health damage
            /// </summary>
            public float HealthDamageFactor;
        }
    }
}

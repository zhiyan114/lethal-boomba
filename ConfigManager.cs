using BepInEx.Configuration;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace LethalBoomba
{
    public static class ConfigManager
    {
        // Config Params
        [FloatValidation(0)]
        public static ConfigEntry<float> Lotta_ZeroValueChance;
        [FloatValidation(0)]
        public static ConfigEntry<float> Lotta_ExplosionChance;
        [FloatValidation(0)]
        public static ConfigEntry<float> Lotta_RandEnemyChance;
        [FloatValidation(0)]
        public static ConfigEntry<float> Lotta_RandTrapChance;
        [FloatValidation(0)]
        public static ConfigEntry<float> Lotta_x1MultiChance;
        [FloatValidation(0)]
        public static ConfigEntry<float> Lotta_x1_5MultiChance;
        [FloatValidation(0)]
        public static ConfigEntry<float> Lotta_x2MultiChance;
        [FloatValidation(0)]
        public static ConfigEntry<float> Lotta_x5MultiChance;
        [FloatValidation(0)]
        public static ConfigEntry<float> Lotta_x10MultiChance;

        [RuntimeInitializeOnLoadMethod]
        static void LoadConf()
        {
            Lotta_ZeroValueChance = Init.config.Bind<float>("lotta.host", "ZeroValueChance", 15f, "The weight/chance for a scratched lotta to have zero value");
            Lotta_ExplosionChance = Init.config.Bind<float>("lotta.host", "ExplosionChance", 15f, "The weight/chance for a scratched lotta to explode");
            Lotta_RandEnemyChance = Init.config.Bind<float>("lotta.host", "RandEnemyChance", 5f, "The weight/chance for a scratched lotta to spawn random enemy");
            Lotta_RandTrapChance = Init.config.Bind<float>("lotta.host", "RandTrapChance", 5f, "The weight/chance for a scratched lotta to spawn random trap");
            Lotta_x1MultiChance = Init.config.Bind<float>("lotta.host", "x1MultiChance", 25f, "The weight/chance for a scratched lotta to have unchanged value");
            Lotta_x1_5MultiChance = Init.config.Bind<float>("lotta.host", "x1_5MultiChance", 20f, "The weight/chance for a scratched lotta to have x1.5 multiplier");
            Lotta_x2MultiChance = Init.config.Bind<float>("lotta.host", "x2MultiChance", 10f, "The weight/chance for a scratched lotta to have x2 multiplier");
            Lotta_x5MultiChance = Init.config.Bind<float>("lotta.host", "x5MultiChance", 3.5f, "The weight/chance for a scratched lotta to have x5 multiplier");
            Lotta_x10MultiChance = Init.config.Bind<float>("lotta.host", "x10MultiChance", 1.5f, "The weight/chance for a scratched lotta to have x10 multiplier");
            CheckAllFieldAndSoftErrorOnFail();
        }

        /// <summary>
        /// This function validates all ConfigEntry field and fallback to the default value and log an error if validation fails
        /// </summary>
        static void CheckAllFieldAndSoftErrorOnFail()
        {
            FieldInfo[] fields = typeof(ConfigManager).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Init.logger.LogInfo($"Checking {fields.Length} configs");
            foreach (FieldInfo field in fields)
            {
                // Only validate config fields
                if (!field.FieldType.IsGenericType) continue;
                if (field.FieldType.GetGenericTypeDefinition() != typeof(ConfigEntry<>)) continue;

                FloatValidation? floatCheck = field.GetCustomAttribute<FloatValidation>(false);
                if (floatCheck != null)
                {
                    // Check for dev errors
                    if (field.FieldType.GetGenericArguments()[0] != typeof(float))
                        throw new Exception($"ConfigManager: FloatValidation applied to non-float config field ({field.Name})");
                    if (floatCheck.checkMax && floatCheck.minVal > floatCheck.maxVal)
                        throw new Exception($"ConfigManager: {field.Name} contain valid validation (min value has higher number than max value)");

                    ConfigEntry<float> fieldVal = (ConfigEntry<float>)field.GetValue(null);
                    
                    if(fieldVal.Value < floatCheck.minVal)
                    {
                        Init.logger.LogError($"ConfigManager: {fieldVal.Definition.Key} ({fieldVal.Definition.Section}) must have a min value of {floatCheck.minVal}, but got {fieldVal.Value}. Falling back to default value.");
                        fieldVal.Value = (float)fieldVal.DefaultValue;
                        continue;
                    }

                    if(floatCheck.checkMax && fieldVal.Value > floatCheck.maxVal)
                    {
                        Init.logger.LogError($"ConfigManager: {fieldVal.Definition.Key} ({fieldVal.Definition.Section}) can only have a max value of {floatCheck.maxVal}, but got {fieldVal.Value}. Falling back to default value.");
                        fieldVal.Value = (float)fieldVal.DefaultValue;
                        continue;
                    }
                }
            }
        }
    }

    // Internal config function used to validate config fields

    [AttributeUsage(AttributeTargets.Field)]
    public class FloatValidation : Attribute
    {
        public float minVal { get; private set; }
        public float maxVal { get; private set; }
        public bool checkMax { get; private set; }
        public FloatValidation(float minVal)
        {
            this.minVal = minVal;
            checkMax = false;
        }

        public FloatValidation(float minVal, float maxVal)
        {
            this.minVal = minVal;
            this.maxVal = maxVal;
            checkMax = true;
        }

    }
}

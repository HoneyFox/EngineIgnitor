using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EngineIgnitor
{
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class EngineIgnitorCore : MonoBehaviour
	{
		public static EngineIgnitorCore s_Singleton = null;
		public static KSP.IO.PluginConfiguration s_Config = null;

		public void Awake()
		{
			LoadSettings();
		}

		public void LoadSettings()
		{
			try
			{
				s_Config = KSP.IO.PluginConfiguration.CreateForType<EngineIgnitorCore>();
				s_Config.load();

				Debug.Log("EngineIgnitor Settings Loaded.");

				UllageSimulator.s_SimulateUllage = s_Config.GetValue<bool>("SimulateUllage");
				UllageSimulator.s_ShutdownEngineWhenUnstable = s_Config.GetValue<bool>("ShutdownWhenUnstable");
				UllageSimulator.s_ExplodeEngineWhenTooUnstable = s_Config.GetValue<bool>("ExplodeWhenTooUnstable");

				UllageSimulator.s_NaturalDiffusionRateX = s_Config.GetValue<float>("CoeffNaturalDiffusionX");
				UllageSimulator.s_NaturalDiffusionRateY = s_Config.GetValue<float>("CoeffNaturalDiffusionY");

				UllageSimulator.s_TranslateAxialCoefficientX = s_Config.GetValue<float>("CoeffTranslateAxialX");
				UllageSimulator.s_TranslateAxialCoefficientY = s_Config.GetValue<float>("CoeffTranslateAxialY");

				UllageSimulator.s_TranslateSidewayCoefficientX = s_Config.GetValue<float>("CoeffTranslateSidewayX");
				UllageSimulator.s_TranslateSidewayCoefficientY = s_Config.GetValue<float>("CoeffTranslateSidewayY");

				UllageSimulator.s_RotateYawPitchCoefficientX = s_Config.GetValue<float>("CoeffRotateYawPitchX");
				UllageSimulator.s_RotateYawPitchCoefficientY = s_Config.GetValue<float>("CoeffRotateYawPitchY");

				UllageSimulator.s_RotateRollCoefficientX = s_Config.GetValue<float>("CoeffRotateRollX");
				UllageSimulator.s_RotateRollCoefficientY = s_Config.GetValue<float>("CoeffRotateRollY");

				UllageSimulator.s_VentingVelocity = s_Config.GetValue<float>("VentingVelocity");
				UllageSimulator.s_VentingAccThreshold = s_Config.GetValue<float>("VentingAccThreshold");
			}
			catch (Exception e)
			{
			//	Debug.Log("Failed to Load EngineIgnitor Settings: " + e.Message);
			}
		}
	}
}

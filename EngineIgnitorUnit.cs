using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EngineIgnitor
{
	public class EngineIgnitorUnit : PartModule
	{
		public static bool s_RequiresEngineerPower = true;
		private static ProtoCrewMember s_HasNotifiedCrewMember = null;
		
		public static List<EngineIgnitorUnit> s_IgnitorPacksOnEva = new List<EngineIgnitorUnit>();

		[KSPField(isPersistant = true)]
		public int ignitors = 1;

		[KSPField(isPersistant = false)]
		public string ignitorType = "type0";

		private StartState m_startState = StartState.None;

		[KSPField(isPersistant = false, guiActive = true, guiName = "IgnitorUnit")]
		private string ignitorUnitState = "type0 - 1";

		public override void OnStart(StartState state)
		{
			m_startState = state;
		}

		public override void OnUpdate()
		{
			if (m_startState != StartState.None && m_startState != StartState.Editor)
			{
				ignitorUnitState = ignitorType + " - " + ((ignitors == -1) ? "Infinite" : ignitors.ToString());

				if (ignitors != 0 && vessel != null)
				{
					ProtoCrewMember crew = vessel.evaController.part.protoModuleCrew[0];
					bool hasEngineerExperienceEffect = crew.experienceTrait.Effects.Exists((Experience.ExperienceEffect effect) => effect is Experience.Effects.EnginePower);
					bool canUseIgnitorUnit = (hasEngineerExperienceEffect || s_RequiresEngineerPower == false);

					if ((s_HasNotifiedCrewMember == null || s_HasNotifiedCrewMember != vessel.evaController.part.protoModuleCrew[0]) && canUseIgnitorUnit == false)
					{
						ScreenMessages.PostScreenMessage("Requires engineer power.", 4.0f, ScreenMessageStyle.UPPER_CENTER);
						s_HasNotifiedCrewMember = vessel.evaController.part.protoModuleCrew[0];
					}
					
					if (vessel.isEVA && vessel.isActiveVessel == true)
					{
						// We are now being grabbed by an EVA I guess...
						if (s_IgnitorPacksOnEva.Contains(this) == false && canUseIgnitorUnit)
							s_IgnitorPacksOnEva.Add(this);
					}
					else
					{
						if (s_IgnitorPacksOnEva.Contains(this) == true)
							s_IgnitorPacksOnEva.Remove(this);
						s_HasNotifiedCrewMember = null;
					}
				}
				else
				{
					if (s_IgnitorPacksOnEva.Contains(this) == true)
						s_IgnitorPacksOnEva.Remove(this);
					s_HasNotifiedCrewMember = null;
				}
			}
		}

		public int Consume(int count)
		{
			if (ignitors == -1)
				return count;

			if (ignitors >= count)
			{
				ignitors -= count;
				return count;
			}
			else
			{
				int ignitorCount = ignitors;
				ignitors = 0;
				return ignitorCount;
			}
		}

		public override string GetInfo()
		{
			return (s_RequiresEngineerPower ? "Requires an engineer Kerbal to use it.\n" : "") + 
				"Contains " + ((ignitors != -1) ? ignitors.ToString() : "infinite") + " " 
				+ ignitorType + " ignitor unit" + ((ignitors == -1 || ignitors > 1) ? "s." : ".");
		}
	}
}
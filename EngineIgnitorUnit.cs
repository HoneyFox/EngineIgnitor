using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EngineIgnitor
{
	public class EngineIgnitorUnit : PartModule
	{
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
					if (vessel.isEVA && vessel.isActiveVessel == true)
					{
						// We are now being grabbed by an EVA I guess...
						if (s_IgnitorPacksOnEva.Contains(this) == false)
							s_IgnitorPacksOnEva.Add(this);
					}
					else
					{
						if (s_IgnitorPacksOnEva.Contains(this) == true)
							s_IgnitorPacksOnEva.Remove(this);
					}
				}
				else
				{
					if (s_IgnitorPacksOnEva.Contains(this) == true)
						s_IgnitorPacksOnEva.Remove(this);
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
			return "Contains " + ((ignitors != -1) ? ignitors.ToString() : "infinite") + " " + ignitorType + " ignitor unit" + ((ignitors == -1 || ignitors > 1) ? "s.\n" : ".\n");
		}
	}
}
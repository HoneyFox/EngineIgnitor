using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EngineIgnitor
{
	public class ModuleExternalIgnitor : PartModule
	{
		public static List<ModuleExternalIgnitor> s_ExternalIgnitors = new List<ModuleExternalIgnitor>();


		[KSPField(isPersistant = false)]
		public bool provideRequiredResources = false;

		[KSPField(isPersistant = false)]
		public string ignitorType = "universal";

		[KSPField(isPersistant = false)]
		public float igniteRange = 1.5f;

		private StartState m_startState = StartState.None;

		public override void OnStart(StartState state)
		{
			m_startState = state;

			if (state != StartState.None && state != StartState.Editor)
			{
				s_ExternalIgnitors.Add(this);
			}
		}

	}
}

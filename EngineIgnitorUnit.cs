using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EngineIgnitor
{
	public class EngineIgnitorUnit : PartModule
	{
		[KSPField(isPersistant = true)]
		public int ignitors = 1;

		private StartState m_startState = StartState.None;
		private Part parentPart = null;
		private ModuleEngineIgnitor parentPartModule = null;

		public override void OnStart(StartState state)
		{
			m_startState = state;

			if (this.part != null && this.part.parent != null)
				parentPart = this.part.parent;

			if (parentPart != null)
			{
				foreach (PartModule module in this.part.parent.Modules)
				{
					if (module is ModuleEngineIgnitor)
					{
						parentPartModule = module as ModuleEngineIgnitor;
						break;
					}
				}
			}
		}

		public override void OnUpdate()
		{
			if (ignitors != 0)
			{	
				if (m_startState != StartState.None && m_startState != StartState.Editor)
				{
					if (parentPartModule != null)
					{
						parentPartModule.ignitionsAvailable += ignitors;
						ignitors = 0;
					}
				}
			}
		}

		public override string GetInfo()
		{
			return "Contains " + ignitors.ToString() + "ignitor unit" + (ignitors > 1 ? "s.\n" : ".\n");
		}
	}
}
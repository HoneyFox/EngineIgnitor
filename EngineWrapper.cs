using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EngineIgnitor
{
	public class EngineWrapper
	{
		public bool isModuleEngineFX = false;
		private ModuleEngines engine = null;
		private ModuleEnginesFX engineFX = null;

		public EngineWrapper(ModuleEngines engine)
		{
			isModuleEngineFX = false;
			this.engine = engine;
		}

		public EngineWrapper(ModuleEnginesFX engineFX)
		{
			isModuleEngineFX = true;
			this.engineFX = engineFX;
		}

		public Vessel vessel
		{
			get
			{
				if(isModuleEngineFX == false)
					return engine.vessel;
				else
					return engineFX.vessel;
			}
		}

		public void SetRunningGroupsActive(bool active)
		{
			if (isModuleEngineFX == false)
				engine.SetRunningGroupsActive(active);
			// Do not need to worry about ModuleEnginesFX.
		}

		public float requestedThrust
		{
			get
			{
				if (isModuleEngineFX == false)
					return engine.requestedThrust;
				else
					return engineFX.requestedThrust;
			}
		}

		public float minThrust
		{
			get 
			{
				if (isModuleEngineFX == false)
					return engine.minThrust;
				else
					return engineFX.minThrust;
			}
		}

		public float maxThrust
		{
			get
			{
				if (isModuleEngineFX == false)
					return engine.maxThrust;
				else
					return engineFX.maxThrust;
			}
		}

		public bool throttleLocked
		{
			get
			{
				if (isModuleEngineFX == false)
					return engine.throttleLocked;
				else
					return engineFX.throttleLocked;
			}
		}

		public List<Propellant> propellants
		{
			get
			{
				if (isModuleEngineFX == false)
					return engine.propellants;
				else
					return engineFX.propellants;
			}
		}

		public Part part
		{
			get
			{
				if (isModuleEngineFX == false)
					return engine.part;
				else
					return engineFX.part;
			}
		}

		public BaseEventList Events
		{
			get
			{
				if (isModuleEngineFX == false)
					return engine.Events;
				else
					return engineFX.Events;
			}
		}

		public void BurstFlameoutGroups()
		{
			if (isModuleEngineFX == false)
				engine.BurstFlameoutGroups();
			else
				engineFX.part.Effects.Event(engineFX.flameoutEffectName);
		}

		public bool allowShutdown
		{
			get
			{
				if (isModuleEngineFX == false)
					return engine.allowShutdown;
				else
					return engineFX.allowShutdown;
			}
		}

		public bool flameout
		{ 
			get
			{
				if (isModuleEngineFX == false)
					return engine.flameout;
				else
					return engineFX.getFlameoutState;
			}
		}
        public bool EngineIgnited
        {
            get
            {
                if (isModuleEngineFX)
                    return engineFX.EngineIgnited;
                else
                    return engine.EngineIgnited;
            }
        }
	}
}

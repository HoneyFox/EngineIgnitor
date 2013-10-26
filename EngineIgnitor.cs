using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EngineIgnitor
{
	[System.Serializable]
	public class IgnitorResource
	{
		public string name;
		public float amount;
		public float currentAmount;

		public IgnitorResource()
		{
		}

		public void Load(ConfigNode node)
		{
			name = node.GetValue("name");

			if (node.HasValue("amount"))
			{
				amount = Mathf.Max(0.0f, float.Parse(node.GetValue("amount")));
			}
		}

		public void Save(ConfigNode node)
		{
			node.AddValue("name", name);
			node.AddValue("amount", Mathf.Max(0.0f, amount));
		}
	}

	public class ModuleEngineIgnitor : PartModule
	{
		public enum EngineIgnitionState
		{
			INVALID = -1,
			NOT_IGNITED = 0,
			HIGH_TEMP = 1,
			IGNITED = 2,
		}
		
		// We can ignite as many times as we want by default.
		// -1: Infinite. 0: Unavailable. 1~...: As is.
		[KSPField(isPersistant = false)]
		public int ignitionsAvailable = -1;

		// Remain ignitionsRemained.
		[KSPField(isPersistant = true)]
		public int ignitionsRemained = -1;

		[KSPField(isPersistant = false, guiActive = true, guiName = "Ignitions")]
		private string ignitionsAvailableString = "Infinite";

		// If we don't have thrust but we still have such temperature then it can auto-ignite when throttle up again.
		[KSPField(isPersistant = false)]
		public float autoIgnitionTemperature = 800;

		[KSPField(isPersistant = false, guiActive = true, guiName = "Auto-Ignite")]
		private string autoIgnitionState = "?/800";

		// In case we have multiple engines...
		[KSPField(isPersistant = false)]
		public int engineIndex = 0;

		[KSPField(isPersistant = false)]
		public string ignitorType = "type0";

		[KSPField(isPersistant = false)]
		public bool useUllageSimulation = true;

		[KSPField(isPersistant = false, guiActive = true, guiName = "Fuel Flow")]
		private string ullageState = "Very Stable";

		// List of all engines. So we can pick the one we are corresponding to.
		private List<ModuleEngines> engines = new List<ModuleEngines>();

		// And that's it.
		private ModuleEngines engine = null;

		// A state for the FSM.
		[KSPField(isPersistant = false, guiActive = true, guiName = "Engine State")]
		private EngineIgnitionState engineState = EngineIgnitionState.INVALID;

		private StartState m_startState = StartState.None;
		private UllageSimulator m_ullageSimulator = new UllageSimulator();
		public List<IgnitorResource> ignitorResources;

		public override void OnStart(StartState state)
		{
			m_startState = state;

			foreach (PartModule module in this.part.Modules)
			{
				if (module is ModuleEngines)
				{
					engines.Add(module as ModuleEngines);
				}
			}
			if (engines.Count > engineIndex)
				engine = engines[engineIndex];
			else
				engine = null;

			if (state == StartState.Editor)
			{
				ignitionsRemained = ignitionsAvailable;
			}

			m_ullageSimulator.Reset();
			if (useUllageSimulation == false || UllageSimulator.s_SimulateUllage == false)
			{
				ullageState = "Very Stable";
			}
		}

		public override void OnAwake()
		{
			base.OnAwake();

			if (ignitorResources == null)
				ignitorResources = new List<IgnitorResource>();
		}

		public override string GetInfo()
		{
			if (ignitionsAvailable != -1)
				return "Can ignite for " + ignitionsAvailable.ToString() + " time(s).\n" + "Ignitor type: " + ignitorType + "\n";
			else
				return "Can ignite for infinite times.\n" + "Ignitor type: " + ignitorType + "\n";
		}

		public bool IsEngineActivated()
		{
			foreach (BaseEvent baseEvent in engine.Events)
			{
				//Debug.Log("Engine's event: " + baseEvent.name);
				if (baseEvent.name.IndexOf("shutdown", StringComparison.CurrentCultureIgnoreCase) >= 0)
				{
					//Debug.Log("IsEngineActivated: " + baseEvent.name + " " + baseEvent.active.ToString() + " " + baseEvent.guiActive.ToString());
					if (baseEvent.active == true)
						return true;
				}
			}

			return false;
		}

		public override void OnUpdate()
		{
			if (m_startState == StartState.None || m_startState == StartState.Editor) return;

			if (ignitionsRemained != -1)
				ignitionsAvailableString = ignitorType + " - " + ignitionsRemained.ToString() + "/" + ignitionsAvailable.ToString();
			else
				ignitionsAvailableString = ignitorType + " - " + "Infinite";

			if (part != null)
				autoIgnitionState = part.temperature.ToString("F1") + "/" + autoIgnitionTemperature.ToString("F1");
			else
				autoIgnitionState = "?/" + autoIgnitionTemperature.ToString("F1");

			if (FlightGlobals.ActiveVessel != null)
			{
				Events["ReloadIgnitor"].guiActiveUnfocused = (FlightGlobals.ActiveVessel.isEVA == true);
				Events["ReloadIgnitor"].guiName = "Reload Ignitor (" + ignitionsAvailableString + ")";
			}

			// Update ullage.
			m_ullageSimulator.Update(this.vessel, this.engine.part, TimeWarp.deltaTime);
			float fuelFlowStability = m_ullageSimulator.GetFuelFlowStability();

			if(useUllageSimulation == true && UllageSimulator.s_SimulateUllage == true)
				ullageState = m_ullageSimulator.GetFuelFlowState();

			if (m_startState == StartState.None || m_startState == StartState.Editor) return;
			if (engine == null) return;
			if (engine.allowShutdown == false) return;

			
			// Record old state.
			EngineIgnitionState oldState = engineState;
			// Decide new state.
			//Debug.Log("Engine: " + engine.requestedThrottle.ToString("F2") + " " + engine.requestedThrust.ToString("F1") + " " + engine.currentThrottle.ToString("F2") + " " + engine.engineShutdown.ToString());
			if (engine.requestedThrust == 0.0f || IsEngineActivated() == false)
			{
				if (engine.part.temperature >= autoIgnitionTemperature)
				{
					engineState = EngineIgnitionState.HIGH_TEMP;
				}
				else
				{
					engineState = EngineIgnitionState.NOT_IGNITED;
				}
			}
			else
			{
				engineState = EngineIgnitionState.IGNITED;
			}

			// This flag is for low-resource state.
			bool preferShutdown = false;

			bool externalIgnitorAvailable = false;
			ModuleExternalIgnitor externalIgnitor = null;
			//Debug.Log("Check all external ignitors: " + ModuleExternalIgnitor.s_ExternalIgnitors.Count.ToString());
			for (int i = 0; i < ModuleExternalIgnitor.s_ExternalIgnitors.Count; ++i)
			{
				ModuleExternalIgnitor itor = ModuleExternalIgnitor.s_ExternalIgnitors[i];
				if (itor.vessel == null || itor.vessel.transform == null || itor.part == null || itor.part.transform == null)
				{
					ModuleExternalIgnitor.s_ExternalIgnitors.RemoveAt(i);
					--i;
				}
			}
			foreach (ModuleExternalIgnitor extIgnitor in ModuleExternalIgnitor.s_ExternalIgnitors)
			{
				if (extIgnitor.vessel == null || extIgnitor.vessel.transform == null || extIgnitor.part == null || extIgnitor.part.transform == null)
					ModuleExternalIgnitor.s_ExternalIgnitors.Remove(extIgnitor);

				//Debug.Log("Iterating external ignitors: " + extIgnitor.vessel.transform.TransformPoint(extIgnitor.part.orgPos).ToString() + " " + engine.vessel.transform.TransformPoint(engine.part.orgPos).ToString());
				if ((extIgnitor.vessel.transform.TransformPoint(extIgnitor.part.orgPos) - engine.vessel.transform.TransformPoint(engine.part.orgPos)).magnitude < extIgnitor.igniteRange)
				{
					if (extIgnitor.ignitorType.Equals("universal", StringComparison.CurrentCultureIgnoreCase) || extIgnitor.ignitorType.Equals(ignitorType, StringComparison.CurrentCultureIgnoreCase))
					{
						//Debug.Log("External Ignitor Found!");
						externalIgnitorAvailable = true;
						externalIgnitor = extIgnitor;
						break;
					}
				}
			}

			// Here comes the state transition process.
			if (oldState == EngineIgnitionState.NOT_IGNITED && engineState == EngineIgnitionState.IGNITED)
			{
				// We need to consume one ignitor to light it up.
				if (ignitionsRemained > 0 || ignitionsRemained == -1 || externalIgnitorAvailable == true)
				{
					if (ignitorResources.Count > 0)
					{
						//Debug.Log("We need to check ignitor resources.");
						// We need to check if we have all ignitor resources.
						float minPotential = 1.0f;
						if (!(externalIgnitorAvailable == true && externalIgnitor.provideRequiredResources == true))
						{
							foreach (IgnitorResource resource in ignitorResources)
							{
								resource.currentAmount = part.RequestResource(resource.name, resource.amount);
								minPotential = Mathf.Min(minPotential, resource.currentAmount / resource.amount);
							}
						}
						else
						{
							if (externalIgnitorAvailable == true)
							{
								externalIgnitor.ConsumeResource();
							}
						}

						if (UllageSimulator.s_SimulateUllage == true && useUllageSimulation == true)
						{
							minPotential *= fuelFlowStability;
						}

						bool ignited = (UnityEngine.Random.Range(0.0f, 1.0f) <= minPotential);
						Debug.Log("Potential = " + minPotential.ToString("F2") + " Ignited: " + ignited.ToString());
						if (ignited == false)
						{
							engineState = EngineIgnitionState.NOT_IGNITED;

							// Low in resources. Prefer to shutdown. Otherwise the ignitor device will be expired.
							//if (minPotential < 0.95f)
							//	preferShutdown = true;

							// Always shutdown the engine if it fails to ignite. player can manually retry.
							preferShutdown = true;
						}
					}
					else
					{
						//Debug.Log("No ignitor resource needed.");
					}

					// The ignitor device has been used no matter the ignition is successful or not.
					if (externalIgnitorAvailable == false)
					{
						if (ignitionsRemained > 0)
						{
							//Debug.Log("Ignitor consumed: " + oldState.ToString() + " " + engineState.ToString() + " " + engine.requestedThrust.ToString("F2") + " " + IsEngineActivated().ToString());
							ignitionsRemained--;
						}
					}
				}
				else if (ignitionsRemained == 0)
				{
					// Oops.
					engineState = EngineIgnitionState.NOT_IGNITED;
				}
				else
				{
					// Oooooops.
					Debug.Log("Invalid Ignitions: " + ignitionsRemained.ToString());
				}
			}
			else if (oldState == EngineIgnitionState.HIGH_TEMP && engineState == EngineIgnitionState.IGNITED)
			{ 
				// Yeah we can auto-ignite without consuming ignitor.
				engineState = EngineIgnitionState.IGNITED;
			}

			// Finally we need to handle the thrust generation. i.e. forcibly shutdown the engine when needed.
			if (engineState == EngineIgnitionState.NOT_IGNITED && ((ignitionsRemained == 0 && externalIgnitorAvailable == false) || preferShutdown == true))
			{
				if (IsEngineActivated() == true)
				{
					vessel.ctrlState.mainThrottle = 0.0f;
					engine.SetRunningGroupsActive(false);
					foreach (BaseEvent baseEvent in engine.Events)
					{
						//Debug.Log("Engine's event: " + baseEvent.name);
						if (baseEvent.name.IndexOf("shutdown", StringComparison.CurrentCultureIgnoreCase) >= 0)
						{
							baseEvent.Invoke();
						}
					}
					engine.SetRunningGroupsActive(false);
				}
			}
			else if (engineState == EngineIgnitionState.IGNITED)
			{
				if (useUllageSimulation == true)
				{
					if (UllageSimulator.s_ShutdownEngineWhenUnstable == true)
					{
						float FuelFlowPotential = Mathf.Pow(fuelFlowStability, 0.03f);
						bool failed = (UnityEngine.Random.Range(0.0f, 1.0f) > FuelFlowPotential);
						if (FuelFlowPotential < 1.0f)
							Debug.Log("FuelFlowPotential = " + FuelFlowPotential.ToString("F2") + " Failed: " + failed.ToString());
						if (failed == true)
						{
							if (UllageSimulator.s_ExplodeEngineWhenTooUnstable == true)
							{
								float ExplodePotential = Mathf.Pow(fuelFlowStability, 0.01f) + 0.01f;
								bool exploded = (UnityEngine.Random.Range(0.0f, 1.0f) > ExplodePotential);
								if (ExplodePotential < 1.0f)
									Debug.Log("ExplodePotential = " + ExplodePotential.ToString("F2") + " Exploded: " + exploded.ToString());
								if (exploded)
								{
									engine.part.explode();
								}
							}

							if (IsEngineActivated() == true)
							{
								vessel.ctrlState.mainThrottle = 0.0f;
								engine.SetRunningGroupsActive(false);
								foreach (BaseEvent baseEvent in engine.Events)
								{
									//Debug.Log("Engine's event: " + baseEvent.name);
									if (baseEvent.name.IndexOf("shutdown", StringComparison.CurrentCultureIgnoreCase) >= 0)
									{
										baseEvent.Invoke();
									}
								}
								engine.SetRunningGroupsActive(false);
							}
						}
					}
				}
			}
		}

		[KSPEvent(name = "ReloadIgnitor", guiName = "Reload Ignitor", active = true, externalToEVAOnly = true, guiActive = false, guiActiveUnfocused = true, unfocusedRange = 3.0f)]
		public void ReloadIgnitor()
		{
			if (ignitionsAvailable == -1 || ignitionsRemained == ignitionsAvailable) return;

			EngineIgnitorUnit availableSource = null;
			foreach (EngineIgnitorUnit unit in EngineIgnitorUnit.s_IgnitorPacksOnEva)
			{
				if (unit.ignitorType.Equals("universal", StringComparison.CurrentCultureIgnoreCase) || unit.ignitorType.Equals(this.ignitorType, StringComparison.CurrentCultureIgnoreCase))
				{
					availableSource = unit;
					break;
				}
			}

			if (availableSource == null)
			{
				if(EngineIgnitorUnit.s_IgnitorPacksOnEva.Count == 0)
					ScreenMessages.PostScreenMessage("No nearby ignitor unit.", 4.0f, ScreenMessageStyle.UPPER_CENTER);
				else
					ScreenMessages.PostScreenMessage("No matched ignitor unit.", 4.0f, ScreenMessageStyle.UPPER_CENTER);
			}
			else
			{
				int ignitionReloaded = availableSource.Consume(ignitionsAvailable - ignitionsRemained);

				if (ignitionsRemained == 0 && ignitionReloaded > 0)
				{
					// We are reloading from empty state. Prefer to activate the engine.
					foreach (BaseEvent baseEvent in engine.Events)
					{
						//Debug.Log("Engine's event: " + baseEvent.name);
						if (baseEvent.name.IndexOf("activate", StringComparison.CurrentCultureIgnoreCase) >= 0)
						{
							baseEvent.Invoke();
						}
					}
				}
				ignitionsRemained += ignitionReloaded;
			}
		}

		public override void OnSave(ConfigNode node)
		{
			foreach (IgnitorResource ignitorResource in ignitorResources)
			{
				ignitorResource.Save(node.AddNode("IGNITOR_RESOURCE"));
			}
			base.OnSave(node);
		}

		public override void OnLoad(ConfigNode node)
		{
			base.OnLoad(node);

			if (ignitorResources != null)
				ignitorResources = new List<IgnitorResource>();

			if (part.partInfo != null)
			{
				ConfigNode origNode = null;

				//Debug.Log(part.partInfo.name);
				foreach (UrlDir.UrlConfig config in GameDatabase.Instance.GetConfigs("PART"))
				{
					//Debug.Log(config.name.Replace("_", "."));
					if (config.name.Replace("_", ".") == part.partInfo.name)
					{
						foreach (ConfigNode configNode in config.config.GetNodes("MODULE"))
						{
							//Debug.Log(configNode.GetValue("name"));
							if (configNode.GetValue("name") == moduleName && (configNode.HasValue("engineIndex") == false || int.Parse(configNode.GetValue("engineIndex")) == engineIndex))
							{
								origNode = configNode;
								break;
							}
						}
						break;
					}
				}

				if (origNode != null)
				{
					//Debug.Log("Original module config node found.");
					foreach (ConfigNode subNode in origNode.GetNodes("IGNITOR_RESOURCE"))
					{
						//Debug.Log("IgnitorResource node found.");
						if (subNode.HasValue("name") == false || subNode.HasValue("amount") == false)
						{
							//Debug.Log("Ignitor Resource must have \'name\' and \'amount\'.");
							continue;
						}
						IgnitorResource newIgnitorResource = new IgnitorResource();
						newIgnitorResource.Load(subNode);
						//Debug.Log("IgnitorResource added: " + newIgnitorResource.name + " " + newIgnitorResource.amount.ToString("F2"));
						ignitorResources.Add(newIgnitorResource);
					}
				}
			}
			//Debug.Log("Total ignitor resources: " + ignitorResources.Count.ToString());

		}
	}
}

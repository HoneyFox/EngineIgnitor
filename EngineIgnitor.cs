using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace EngineIgnitor
{
	[Serializable]
	public class IgnitorResource : IConfigNode
	{
		[SerializeField]
		public string name;
		[SerializeField]
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

		public override string ToString()
		{
			return name + "(" + amount.ToString("F3") + ")";
		}

		public static IgnitorResource FromString(string str)
		{
			IgnitorResource ir = new IgnitorResource();
			int indexL = str.LastIndexOf('('); int indexR = str.LastIndexOf(')');
			ir.name = str.Substring(0, indexL);
			ir.amount = float.Parse(str.Substring(indexL + 1, indexR - indexL - 1));
			return ir;
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

		[KSPField(isPersistant = false)]
		public bool isPressureFed = false;

		[KSPField(isPersistant = false, guiActive = true, guiName = "Fuel Flow")]
		private string ullageState = "Very Stable";

		// List of all engines. So we can pick the one we are corresponding to.
		private List<EngineWrapper> engines = new List<EngineWrapper>();

		// And that's it.
		private EngineWrapper engine = null;

		// A state for the FSM.
		[KSPField(isPersistant = false, guiActive = true, guiName = "Engine State")]
		private EngineIgnitionState engineState = EngineIgnitionState.INVALID;

		private StartState m_startState = StartState.None;
		private bool m_isEngineMouseOver = false;
		private UllageSimulator m_ullageSimulator = new UllageSimulator();

		public List<string> ignitorResourcesStr;
		public List<IgnitorResource> ignitorResources;

		public override void OnStart(StartState state)
		{
			m_startState = state;

			engines.Clear();
			foreach (PartModule module in this.part.Modules)
			{
				if (module is ModuleEngines)
				{
					engines.Add(new EngineWrapper(module as ModuleEngines));
				}
				else if (module is ModuleEnginesFX)
				{
					engines.Add(new EngineWrapper(module as ModuleEnginesFX));
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

			//Debug.Log("Restoring them from strings.");
			ignitorResources.Clear();
			foreach (string str in ignitorResourcesStr)
			{
				ignitorResources.Add(IgnitorResource.FromString(str));
			}
		}

		public override void OnAwake()
		{
			base.OnAwake();

			if (ignitorResources == null)
				ignitorResources = new List<IgnitorResource>();
			if (ignitorResourcesStr == null)
				ignitorResourcesStr = new List<string>();
		}

		public override string GetInfo()
		{
			if (ignitionsAvailable != -1)
				return "Can ignite for " + ignitionsAvailable.ToString() + " time(s).\n" + "Ignitor type: " + ignitorType + "\n";
			else
				return "Can ignite for infinite times.\n" + "Ignitor type: " + ignitorType + "\n";
		}

		public void OnMouseEnter()
		{
			if (HighLogic.LoadedSceneIsEditor)
			{
				//Debug.Log("ModuleEngineIgnitor: OnMouseEnter()");
				m_isEngineMouseOver = true;
			}
		}

		public void OnMouseExit()
		{
			if (HighLogic.LoadedSceneIsEditor)
			{
				//Debug.Log("ModuleEngineIgnitor: OnMouseExit()");
				m_isEngineMouseOver = false;
			}
		}

		void OnGUI()
		{
			//Debug.Log("ModuleEngineIgnitor: OnGUI() " + ignitorResources.Count.ToString());
			if (m_isEngineMouseOver == false) return;

			string ignitorInfo = "Ignitor: ";
			if (ignitionsRemained == -1)
				ignitorInfo += ignitorType + "(Infinite).";
			else
				ignitorInfo += ignitorType + "(" + ignitionsRemained.ToString() + ").";

			string resourceRequired = "No resource requirement for ignition.";
			if(ignitorResources.Count > 0)
			{
				resourceRequired = "Ignition requires: ";
				for(int i = 0; i < ignitorResources.Count; ++i)
				{
					IgnitorResource resource = ignitorResources[i];
					resourceRequired += resource.name + "(" + resource.amount.ToString("F3") + ")";
					if(i != ignitorResources.Count - 1)
					{
						resourceRequired += ", ";
					}
					else
					{
						resourceRequired += ".";
					}
				}
			}

			string ullageInfo = "";
			if (useUllageSimulation == true && UllageSimulator.s_SimulateUllage == true)
			{
				ullageInfo = "Need settling down fuel before ignition.";
				if (isPressureFed == true)
				{
					bool fuelPressurized = true;
					foreach(Propellant p in engine.propellants)
					{
						bool foundPressurizedSource = false;
						List<PartResource> resourceSources = new List<PartResource>();
						engine.part.GetConnectedResources(p.id, resourceSources);
						foreach (PartResource pr in resourceSources)
						{
							//Debug.Log("Propellant: " + pr.resourceName + " " + IsModularFuelTankPressurizedFor(pr).ToString());
							if (IsModularFuelTankPressurizedFor(pr) == true)
							{
								foundPressurizedSource = true;
								break;
							}
						}

						if (foundPressurizedSource == false)
						{
							fuelPressurized = false;
							break;
						}
					}
					ullageInfo = "Pressure fed. " + (fuelPressurized ? "Pressurized fuel tank(s) connected." : "No pressurized fuel tank containing required resource(s) connected.");
				}
			}
			else
			{
				ullageInfo = "Ullage simulation disabled.";
			}

			Vector2 screenCoords = Camera.main.WorldToScreenPoint(part.transform.position);
			Rect ignitorInfoRect = new Rect(screenCoords.x - 100.0f, Screen.height - screenCoords.y - 30.0f, 200.0f, 20.0f);
			GUIStyle ignitorInfoStyle = new GUIStyle();
			ignitorInfoStyle.alignment = TextAnchor.MiddleCenter;
			ignitorInfoStyle.normal.textColor = Color.red;
			GUI.Label(ignitorInfoRect, ignitorInfo, ignitorInfoStyle);
			Rect ignitorResourceListRect = new Rect(screenCoords.x - 100.0f, Screen.height - screenCoords.y - 10.0f, 200.0f, 20.0f);
			GUIStyle listStyle = new GUIStyle();
			listStyle.alignment = TextAnchor.MiddleCenter;
			listStyle.normal.textColor = Color.red;
			GUI.Label(ignitorResourceListRect, resourceRequired, listStyle);
			Rect ullageInfoRect = new Rect(screenCoords.x - 100.0f, Screen.height - screenCoords.y + 10.0f, 200.0f, 20.0f);
			GUIStyle ullageInfoStyle = new GUIStyle();
			ullageInfoStyle.alignment = TextAnchor.MiddleCenter;
			ullageInfoStyle.normal.textColor = Color.blue;
			GUI.Label(ullageInfoRect, ullageInfo, ullageInfoStyle);

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
			float boilOffAcc = GetAccelerationOfMFSFuelBoilOff();
			m_ullageSimulator.Update(this.vessel, this.engine.part, TimeWarp.deltaTime, boilOffAcc);
			float fuelFlowStability = m_ullageSimulator.GetFuelFlowStability();

			bool fuelPressurized = true;
			foreach(Propellant p in engine.propellants)
			{
				bool foundPressurizedSource = false;
				List<PartResource> resourceSources = new List<PartResource>();
				engine.part.GetConnectedResources(p.id, resourceSources);
				foreach (PartResource pr in resourceSources)
				{
					//Debug.Log("Propellant: " + pr.resourceName + " " + IsModularFuelTankPressurizedFor(pr).ToString());
					if (IsModularFuelTankPressurizedFor(pr) == true)
					{
						foundPressurizedSource = true;
						break;
					}
				}

				if (foundPressurizedSource == false)
				{
					fuelPressurized = false;
					break;
				}
			}

			if (useUllageSimulation == true && UllageSimulator.s_SimulateUllage == true)
			{
				if (isPressureFed == true)
				{
					if (fuelPressurized == true)
					{
						ullageState = "Pressurized";
						fuelFlowStability = 1.0f;
					}
					else
					{
						ullageState = "Unpressurized";
						fuelFlowStability = 0.0f;
					}
				}
				else
				{
					if (fuelPressurized == true)
					{
						ullageState = "Very Stable";
						fuelFlowStability = 1.0f;
					}
					else
					{
						ullageState = m_ullageSimulator.GetFuelFlowState();
					}
				}
			}
			else
			{
				if (isPressureFed == true)
					ullageState = "Pressurized";
				else
					ullageState = "Very Stable";
				fuelFlowStability = 1.0f;
			}

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
				bool inRange = (extIgnitor.vessel.transform.TransformPoint(extIgnitor.part.orgPos) - engine.vessel.transform.TransformPoint(engine.part.orgPos)).magnitude < extIgnitor.igniteRange;
				bool isAttached = false;
				foreach(AttachNode attachNode in extIgnitor.part.attachNodes)
				{
					if(attachNode.attachedPart == engine.part)
					{
						isAttached = true;
						break;
					}
				}
				foreach (Part childPart in extIgnitor.part.children)
				{
					if (childPart == engine.part)
					{
						isAttached = true;
						break;
					}
				}
				if (extIgnitor.attachedPart == engine.part)
				{
					isAttached = true;
				}

				if (inRange || isAttached)
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
								Debug.Log("Resource (" + resource.name + ") = " + resource.currentAmount.ToString("F2") + "/" + resource.amount.ToString("F2"));
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
							//Debug.Log("FuelFlowStability = " + fuelFlowStability.ToString("F3"));
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
					engine.BurstFlameoutGroups();
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
								engine.BurstFlameoutGroups();
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

				// Never mind...

				//// Try to fix the cheaty way of connecting a small pressurized tank at the bottom of a big unpressurized tank.
				//if (UllageSimulator.s_SimulateUllage == true && isPressureFed == true)
				//{
				//    if (IsEngineActivated() == true)
				//    {
				//        // part.FindResource_StackPriority(Part origin, List<PartResource> sources, int resourceID, double demand, int requestID)
				//        foreach (Propellant propellant in engine.propellants)
				//        {
				//            List<PartResource> sources = new List<PartResource>();
				//            MethodInfo mi = this.part.GetType().GetMethod("FindResource_StackPriority", BindingFlags.NonPublic | BindingFlags.Instance);
				//            mi.Invoke(this.part, new object[] {this.part, sources, propellant.id, double.MaxValue, Part.NewRequestID()});

				//            // Now all sources are here and should be in a certain order.
				//            string output = "Engine: " + this.part.partInfo.title + "\n";
				//            foreach (PartResource pr in sources)
				//            {
				//                output += "  Resource: " + pr.resourceName + "\n";
				//                output += "    " + pr.part.partName + "( *" + pr.part.symmetryCounterparts.Count.ToString() + " ): ";
				//                output += pr.amount.ToString("F1") + "/" + pr.maxAmount.ToString("F1") + "\n";
				//            }
				//            Debug.Log(output);
				//        }
				//    }
				//}
			}
		}

		[KSPEvent(name = "ReloadIgnitor", guiName = "Reload Ignitor", active = true, externalToEVAOnly = true, guiActive = false, guiActiveUnfocused = true, unfocusedRange = 3.0f)]
		public void ReloadIgnitor()
		{
			if (ignitionsAvailable == -1 || ignitionsRemained == ignitionsAvailable) return;

			EngineIgnitorUnit availableSource = null;
			for(int i = 0; i < EngineIgnitorUnit.s_IgnitorPacksOnEva.Count; ++i)
			{
				EngineIgnitorUnit unit = EngineIgnitorUnit.s_IgnitorPacksOnEva[i];
				if (unit.vessel != null && unit.vessel.isActiveVessel == true && unit.vessel.isEVA == true && unit.ignitors != 0)
				{
					if (unit.ignitorType.Equals("universal", StringComparison.CurrentCultureIgnoreCase) || unit.ignitorType.Equals(this.ignitorType, StringComparison.CurrentCultureIgnoreCase))
					{
						availableSource = unit;
					}
				}
				else
				{
					EngineIgnitorUnit.s_IgnitorPacksOnEva.Remove(unit);
					i--;
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

		public bool IsModularFuelTankPressurizedFor(PartResource pr)
		{
			if (pr.part != null)
			{
				if (pr.part.Modules.Contains("ModuleFuelTanks"))
				{
					PartModule mfsModule = pr.part.Modules["ModuleFuelTanks"];
					FieldInfo dictFieldInfo = (mfsModule.GetType().GetField("pressurizedFuels"));
					Dictionary<string, bool> dict = (Dictionary<string, bool>)dictFieldInfo.GetValue(mfsModule);

					if (dict.ContainsKey(pr.resourceName) == false)
					{
						//Debug.Log("No " + pr.resourceName + " resource in this tank.");
						return false;
					}
					else
					{
						//Debug.Log(dict[pr.resourceName].ToString() + " " + pr.amount.ToString("F2"));
						return (dict[pr.resourceName] && (pr.amount > 0.0));
					}
				}
				else
				{
					// This fuel tank doesn't seem to have MFS module.
					//Debug.Log("No MFS found.");
					return false;
				}
			}
			return false;
		}

		public float GetAccelerationOfMFSFuelBoilOff()
		{
			if (vessel != null)
			{
				double massRate = 0.0f;
				foreach (Part part in vessel.Parts)
				{
					if (part.Modules.Contains("ModuleFuelTanks"))
					{
						PartModule mfsModule = part.Modules["ModuleFuelTanks"];
						FieldInfo fuelListFieldInfo = (mfsModule.GetType().GetField("fuelList"));
						object listObj = (fuelListFieldInfo.GetValue(mfsModule));
						int count = (int)(listObj.GetType().GetProperty("Count").GetValue(listObj, null));
						for(int i = 0; i < count; ++i)
						{
							object obj = listObj.GetType().GetProperty("Item").GetValue(listObj, new object[] { i });

							string resourceName = (string)(obj.GetType().GetField("name").GetValue(obj));
							double loss_rate = (double)(obj.GetType().GetField("loss_rate").GetValue(obj));
							double amount = (double)(obj.GetType().GetProperty("amount").GetValue(obj, null));
							double maxAmount = (double)(obj.GetType().GetProperty("maxAmount").GetValue(obj, null));
							float temperature = (float)(obj.GetType().GetField("temperature").GetValue(obj));

							if (amount > 0 && loss_rate > 0 && part.temperature > temperature)
							{
								double loss = maxAmount * loss_rate * (part.temperature - temperature); // loss_rate is calibrated to 300 degrees.
								if (loss > amount)
									loss = amount;

								massRate += loss * PartResourceLibrary.Instance.GetDefinition(resourceName).density;
							}
						}
					}
				}

				return Convert.ToSingle(massRate) * UllageSimulator.s_VentingVelocity / vessel.GetTotalMass();
			}
			else
				return 0.0f;
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
			
			//if (ignitorResources != null)
			ignitorResourcesStr = new List<string>();
			ignitorResources = new List<IgnitorResource>();

			foreach (ConfigNode subNode in node.GetNodes("IGNITOR_RESOURCE"))
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
				ignitorResourcesStr.Add(newIgnitorResource.ToString());
			}

			#region Old and wrong codes...
			/*
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
			*/
			#endregion
		}
	}
}

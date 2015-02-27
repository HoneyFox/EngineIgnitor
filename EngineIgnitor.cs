using System;
using System.Collections;
using System.Collections.Generic;
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

        static string FormatTime(double time) // borrowed from DRE.
        {
            int iTime = (int)time % 3600;
            int seconds = iTime % 60;
            int minutes = (iTime / 60) % 60;
            int hours = (iTime / 3600);
            return hours.ToString("D2")
            + ":" + minutes.ToString("D2") + ":" + seconds.ToString("D2");
        }

        public void Start()
        {
            vParts = -1;
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

		public override void OnStart(StartState state)
		{
			m_startState = state;

			if (state == StartState.Editor)
			{
				ignitionsRemained = ignitionsAvailable;
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

            UpdateRF(false);

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
				ullageInfo = "Need settling down fuel before ignition.";
			else
                ullageInfo = "Ullage simulation disabled.";
            if (isPressureFed == true)
            {
                bool fuelPressurized = true;
                foreach (Propellant p in engine.propellants)
                {
                    bool foundPressurizedSource = false;
                    List<PartResource> resourceSources = new List<PartResource>();
                    engine.part.GetConnectedResources(p.id, p.GetFlowMode(), resourceSources);
                    foreach (PartResource pr in resourceSources)
                    {
                        //Debug.Log("Propellant: " + pr.resourceName + " " + IsModularFuelTankPressurizedFor(pr).ToString());
                        if (RFIsPressurized(pr) == true)
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
                ullageInfo += ", Pressure fed. " + (fuelPressurized ? "Pressurized fuel tank(s) connected." : "No pressurized fuel tank containing required resource(s) connected.");
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
			/*foreach (BaseEvent baseEvent in engine.Events)
			{
				//Debug.Log("Engine's event: " + baseEvent.name);
				if (baseEvent.name.IndexOf("shutdown", StringComparison.CurrentCultureIgnoreCase) >= 0)
				{
					//Debug.Log("IsEngineActivated: " + baseEvent.name + " " + baseEvent.active.ToString() + " " + baseEvent.guiActive.ToString());
					if (baseEvent.active == true)
						return true;
				}
			}

			return false;*/
            return engine.EngineIgnited;
		}

		public void FixedUpdate()
		{
            if (!HighLogic.LoadedSceneIsFlight)
                return;

			if (m_startState == StartState.None || m_startState == StartState.Editor || (object)engine == null) return;

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

            UpdateRF(); // Update cached tanks

			// Update ullage.
			float boilOffAcc = RFBoiloffAcceleration();
			
			bool fuelPressurized = true;
			float minFuelRatio = 1.0f;
			foreach(Propellant p in engine.propellants)
			{
				double fuelAmount = 0.0;
				double fuelMaxAmount = 0.0;

				bool foundPressurizedSource = false;
				List<PartResource> resourceSources = new List<PartResource>();
				engine.part.GetConnectedResources(p.id, p.GetFlowMode(), resourceSources);
				foreach (PartResource pr in resourceSources)
				{
					//Debug.Log("Propellant: " + pr.resourceName + " " + IsModularFuelTankPressurizedFor(pr).ToString());
					if (foundPressurizedSource == false && RFIsPressurized(pr) == true)
					{
						foundPressurizedSource = true;
					}

					fuelAmount += pr.amount;
					fuelMaxAmount += pr.maxAmount;
				}
				
				if (minFuelRatio > fuelAmount / fuelMaxAmount)
					minFuelRatio = Convert.ToSingle(fuelAmount / fuelMaxAmount);

				if (foundPressurizedSource == false)
				{
					fuelPressurized = false;
				}
			}

			m_ullageSimulator.Update(this.vessel, this.engine.part, TimeWarp.fixedDeltaTime, boilOffAcc, minFuelRatio);
			float fuelFlowStability = m_ullageSimulator.GetFuelFlowStability(minFuelRatio);
			
			if (useUllageSimulation == true && UllageSimulator.s_SimulateUllage == true)
			{
                ullageState = m_ullageSimulator.GetFuelFlowState();
			}
			else
			{
			    ullageState = "Very Stable";
				fuelFlowStability = 1.0f;
			}
            if (isPressureFed)
            {
                if (fuelPressurized == true)
                {
                    ullageState += ", Pressurized";
                }
                else
                {
                    ullageState += ", Unpressurized";
                    fuelFlowStability = 0.0f;
                }
            }

			
			// Record old state.
			EngineIgnitionState oldState = engineState;
			// Decide new state.
			//Debug.Log("Engine: " + engine.requestedThrust.ToString("F4"));
			if (engine.requestedThrust <= 0.0f || engine.flameout == true || (IsEngineActivated() == false && engine.allowShutdown == true))
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
				if (oldState != EngineIgnitionState.IGNITED)
				{
					// When changing from not-ignited to ignited, we must ensure that the throttle is non-zero.
					// Or if the throttle is locked. (SRBs)
					if (vessel.ctrlState.mainThrottle > 0.0f || engine.throttleLocked == true)
					{
						engineState = EngineIgnitionState.IGNITED;
					}
				}
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
								//Debug.Log("Resource (" + resource.name + ") = " + resource.currentAmount.ToString("F2") + "/" + resource.amount.ToString("F2"));
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
						//Debug.Log("Potential = " + minPotential.ToString("F2") + " Ignited: " + ignited.ToString());
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
						/*if (FuelFlowPotential < 1.0f)
							Debug.Log("FuelFlowPotential = " + FuelFlowPotential.ToString("F2") + " Failed: " + failed.ToString());*/
						if (failed == true)
						{
                            FlightLogger.eventLog.Add("[" + FormatTime(vessel.missionTime) + "] " + part.partInfo.title + " had pressurant in its feed line and shut down.");
                            bool exploded = false;
							if (UllageSimulator.s_ExplodeEngineWhenTooUnstable == true)
							{
								float ExplodePotential = Mathf.Pow(fuelFlowStability, 0.01f) + 0.01f;
								exploded = (UnityEngine.Random.Range(0.0f, 1.0f) > ExplodePotential);
								/*if (ExplodePotential < 1.0f)
									Debug.Log("ExplodePotential = " + ExplodePotential.ToString("F2") + " Exploded: " + exploded.ToString());*/
								if (exploded)
								{
                                    FlightLogger.eventLog.Add("[" + FormatTime(vessel.missionTime) + "] " + part.partInfo.title + " exploded from unstable flow.");
									engine.part.explode();
								}
							}

							if (!exploded && IsEngineActivated() == true)
							{
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

        // RF interaction
        int vParts = -1;
        static Type rfModule, rfTank;
        static FieldInfo RFname, RFloss_rate, RFtemperature, RFfuelList, RFpressurizedFuels;
        static bool noRFfields = true;
        static bool noRFTankfields = true;
        struct RFTank
        {
            public string name;
            public double rate;
            public float temp;
            public bool pFed;
        }
        Dictionary<Part, Dictionary<string, RFTank>> vesselTanks = null;
        public void UpdateRF(bool inFlight = true)
        {
            int curParts = -1;
            if (inFlight)
            {
                if (vessel == null)
                {
                    vParts = -1;
                    return;
                }
                else
                    curParts = vessel.Parts.Count;
            }
            else
            {
                if (EditorLogic.SortedShipList.Count == 0)
                {
                    vParts = -1;
                    return;
                }
                else
                    curParts = EditorLogic.SortedShipList.Count;
            }
            if (vesselTanks == null || vParts != curParts)
            {
                vesselTanks = new Dictionary<Part, Dictionary<string, RFTank>>();
                List<PartModule> rfModules = new List<PartModule>();
                if (inFlight)
                {
                    for (int i = 0; i < vessel.Parts.Count; i++)
                    {
                        if (!vessel.Parts[i].Modules.Contains("ModuleFuelTanks"))
                            continue;
                        if (noRFfields)
                        {
                            rfModule = vessel.Parts[i].Modules["ModuleFuelTanks"].GetType();
                            RFfuelList = rfModule.GetField("fuelList");
                            RFpressurizedFuels = rfModule.GetField("pressurizedFuels");
                            noRFfields = false;
                        }
                        PartModule mfsModule = vessel.Parts[i].Modules["ModuleFuelTanks"];
                        rfModules.Add(mfsModule);
                    }
                }
                else
                {
                    // Yes, copypasta code >.>
                    for (int i = 0; i < EditorLogic.SortedShipList.Count; i++)
                    {
                        if (!EditorLogic.SortedShipList[i].Modules.Contains("ModuleFuelTanks"))
                            continue;
                        if (noRFfields)
                        {
                            rfModule = EditorLogic.SortedShipList[i].Modules["ModuleFuelTanks"].GetType();
                            RFfuelList = rfModule.GetField("fuelList");
                            RFpressurizedFuels = rfModule.GetField("pressurizedFuels");
                            noRFfields = false;
                        }
                        PartModule mfsModule = EditorLogic.SortedShipList[i].Modules["ModuleFuelTanks"];
                        rfModules.Add(mfsModule);
                    }
                }
                for (int i = 0; i < rfModules.Count; i++)
                {
                    IEnumerable tankList = (IEnumerable)RFfuelList.GetValue(rfModules[i]);
                    Dictionary<string, bool> pfed = (Dictionary<string, bool>)RFpressurizedFuels.GetValue(rfModules[i]);
                    if (noRFTankfields)
                    {
                        var obj = tankList.GetEnumerator().MoveNext();
                        rfTank = obj.GetType();
                        RFname = rfTank.GetField("name");
                        RFloss_rate = rfTank.GetField("loss_rate");
                        RFtemperature = rfTank.GetField("temperature");
                        noRFTankfields = false;
                    }
                    Dictionary<string, RFTank> tanks = new Dictionary<string, RFTank>();
                    foreach (var obj in tankList)
                    {
                        RFTank tank;
                        tank.name = (string)(RFname.GetValue(obj));
                        tank.rate = (double)(RFloss_rate.GetValue(obj));
                        tank.temp = (float)(RFtemperature.GetValue(obj));
                        if (pfed.ContainsKey(tank.name) && pfed[tank.name])
                            tank.pFed = true;
                        else
                            tank.pFed = false;
                        tanks[tank.name] = tank;
                    }
                }
            }
            vParts = vessel.Parts.Count;
        }

        public bool RFIsPressurized(PartResource pr)
        {
            if (pr.part == null || pr.amount <= 0)
                return false;
            if (vesselTanks.ContainsKey(pr.part))
                if (vesselTanks[pr.part].ContainsKey(pr.resourceName))
                    return vesselTanks[pr.part][pr.resourceName].pFed;
            return false;
        }
	    public float RFBoiloffAcceleration()
		{
		    if (vessel == null)
		        return 0.0f;

		    double massRate = 0.0f;
            foreach(Dictionary<string, RFTank> tanks in vesselTanks.Values)
            {
		        foreach(RFTank tank in tanks.Values)
		        {
                    PartResource r = part.Resources[tank.name];
                    double amount = r.amount;
		            double maxAmount = r.maxAmount;
		            if (amount > 0 && tank.rate > 0 && part.temperature > tank.temp)
		            {
		                double loss = maxAmount*tank.rate*(part.temperature - tank.temp); // loss_rate is calibrated to 300 degrees.
		                if (loss > amount)
		                    loss = amount;

                        massRate += loss * r.info.density;
		            }
		        }
		    }

		    return Convert.ToSingle(massRate)*UllageSimulator.s_VentingVelocity/vessel.GetTotalMass();
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
		}
	}
}

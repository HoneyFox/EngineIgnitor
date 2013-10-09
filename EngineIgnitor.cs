using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EngineIgnitor
{
	public class ModuleEngineIgnitor : PartModule
	{
		public enum EngineIgnitionState
		{
			INVALID = -1,
			NOT_IGNITED = 0,
			HIGH_TEMP = 1,
			IGNITED = 2,
		}

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

		// We can ignite as many times as we want by default.
		// -1: Infinite. 0: Unavailable. 1~...: As is.
		[KSPField(isPersistant = true)]
		public float ignitionsAvailable = -1;

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

		// List of all engines. So we can pick the one we are corresponding to.
		private List<ModuleEngines> engines = new List<ModuleEngines>();

		// And that's it.
		private ModuleEngines engine = null;

		// A state for the FSM.
		[KSPField(isPersistant = false, guiActive = true, guiName = "Engine State")]
		private EngineIgnitionState engineState = EngineIgnitionState.INVALID;

		private StartState m_startState = StartState.None;
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
				return "Can ignite for " + ignitionsAvailable.ToString() + " time(s).\n";
			else
				return "Can ignite for infinite times.\n";
		}

		public override void OnUpdate()
		{
			if (m_startState == StartState.None || m_startState == StartState.Editor) return;

			if (ignitionsAvailable != -1)
				ignitionsAvailableString = ignitionsAvailable.ToString();
			else
				ignitionsAvailableString = "Infinite";

			if (part != null)
				autoIgnitionState = part.temperature.ToString("F1") + "/" + autoIgnitionTemperature.ToString("F1");
			else
				autoIgnitionState = "?/" + autoIgnitionTemperature.ToString("F1");

			if (m_startState == StartState.None || m_startState == StartState.Editor) return;
			if (engine == null) return;
			if (engine.allowShutdown == false) return;

			// Record old state.
			EngineIgnitionState oldState = engineState;
			// Decide new state.
			if (engine.requestedThrust == 0.0f || engine.engineShutdown == true)
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

			// Here comes the state transition process.
			if (oldState == EngineIgnitionState.NOT_IGNITED && engineState == EngineIgnitionState.IGNITED)
			{
				// We need to consume one ignitor to light it up.
				if (ignitionsAvailable > 0 || ignitionsAvailable == -1)
				{
					if (ignitorResources.Count > 0)
					{
						//Debug.Log("We need to check ignitor resources.");
						// We need to check if we have all ignitor resources.
						float minPotential = 1.0f;
						foreach (IgnitorResource resource in ignitorResources)
						{
							resource.currentAmount = part.RequestResource(resource.name, resource.amount);
							minPotential = Mathf.Min(minPotential, resource.currentAmount / resource.amount);
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
					if(ignitionsAvailable > 0)
						ignitionsAvailable--;
				}
				else if (ignitionsAvailable == 0)
				{
					// Oops.
					engineState = EngineIgnitionState.NOT_IGNITED;
				}
				else
				{ 
					// Oooooops.
					Debug.Log("Invalid IgnitionsAvaiable: " + ignitionsAvailable.ToString());
				}
			}
			else if (oldState == EngineIgnitionState.HIGH_TEMP && engineState == EngineIgnitionState.IGNITED)
			{ 
				// Yeah we can auto-ignite without consuming ignitor.
				engineState = EngineIgnitionState.IGNITED;
			}

			// Finally we need to handle the thrust generation. i.e. forcibly shutdown the engine when needed.
			if (engineState == EngineIgnitionState.NOT_IGNITED && (ignitionsAvailable == 0 || preferShutdown == true))
			{
				foreach (BaseEvent baseEvent in engine.Events)
				{
					//Debug.Log("Engine's event: " + baseEvent.name);
					if (baseEvent.name.IndexOf("shutdown", StringComparison.CurrentCultureIgnoreCase) >= 0)
					{
						baseEvent.Invoke();
					}
				}
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

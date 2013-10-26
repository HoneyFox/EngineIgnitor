using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EngineIgnitor
{
	public class ModuleExternalIgnitor : PartModule
	{
		public static List<ModuleExternalIgnitor> s_ExternalIgnitors = new List<ModuleExternalIgnitor>();

		// We can ignite as many times as we want by default.
		// -1: Infinite. 0: Unavailable. 1~...: As is.
		[KSPField(isPersistant = false)]
		public int ignitionsAvailable = -1;

		// Remain ignitionsRemained.
		[KSPField(isPersistant = true)]
		public int ignitionsRemained = -1;

		[KSPField(isPersistant = false, guiActive = true, guiName = "Ignitions")]
		private string ignitionsAvailableString = "Infinite";

		// Whether we use our own resources or the vessel's.
		[KSPField(isPersistant = false)]
		public bool provideRequiredResources = false;

		[KSPField(isPersistant = false)]
		public string ignitorType = "universal";

		[KSPField(isPersistant = false)]
		public float igniteRange = 1.5f;

		private StartState m_startState = StartState.None;
		public List<IgnitorResource> ignitorResources;

		public override void OnStart(StartState state)
		{
			m_startState = state;

			if (state != StartState.None && state != StartState.Editor)
			{
				if(s_ExternalIgnitors.Contains(this) == false)
					s_ExternalIgnitors.Add(this);
			}

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
		}

		public override void OnUpdate()
		{
			if (m_startState != StartState.None && m_startState != StartState.Editor)
			{
				if (ignitionsRemained != -1)
					ignitionsAvailableString = ignitorType + " - " + ignitionsRemained.ToString() + "/" + ignitionsAvailable.ToString();
				else
					ignitionsAvailableString = ignitorType + " - " + "Infinite";

				if (this.vessel == null)
				{
					s_ExternalIgnitors.Remove(this);
				}
				else
				{
					bool enoughIgnitorResources = true;
					if (provideRequiredResources == true && ignitorResources.Count > 0)
					{
						foreach (IgnitorResource resource in ignitorResources)
						{
							resource.currentAmount = Convert.ToSingle(part.Resources[resource.name].amount);
							if (resource.currentAmount < resource.amount)
							{
								enoughIgnitorResources = false;
								break;
							}
						}
					}

					if ((ignitionsRemained == -1 || ignitionsRemained > 0) && enoughIgnitorResources)
					{
						if (s_ExternalIgnitors.Contains(this) == false)
							s_ExternalIgnitors.Add(this);
					}
					else
					{
						s_ExternalIgnitors.Remove(this);
					}
				}
			}
		}

		public void ConsumeResource()
		{
			foreach (IgnitorResource resource in ignitorResources)
			{
				part.Resources[resource.name].amount = Math.Max(0.0f, part.Resources[resource.name].amount - resource.amount);
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
							if (configNode.GetValue("name") == moduleName)
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

		public override string GetInfo()
		{
			if (ignitionsAvailable != -1)
				return "Can ignite for " + ignitionsAvailable.ToString() + " time(s).\n" + "Ignitor type: " + ignitorType + "\n";
			else
				return "Can ignite for infinite times.\n" + "Ignitor type: " + ignitorType + "\n";
		}
	}
}

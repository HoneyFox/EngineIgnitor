Engine Ignitor
==================================
You may need to add modules to your engines if it's not included in the Module Manager configs.
This pack has provided some config of main-stream rocket mod packs used by Module Manager so that more engines will be affected. (Or maybe you don't like that cuz it's making the game too hard?)

==================================
Credit:
	Stock parts' Module Manager config file:
		Credited to CoriW. I've done some minor modification afterwards though.
	Radial HypergolicFluid tank:
		New texture and part.cfg credited to CoriW.
		Model credited to Squad.
		It's modified to have "TechRequired = start" for career mode. Don't forget to unlock this part in your R&D center.
        Ignitor Unit Toolbox:
		New texture and part.cfg credited to Tyrador.
                Model credited to Squad.
		It's also located in the "Start" tech node. Unlock it if you do want to use it.

==================================
1) ModuleEngineIgnitor

Code to add to part.cfg of engines:

MODULE
{
	name = ModuleEngineIgnitor
	ignitionsAvailable = 4
	autoIgnitionTemperature = 800
	ignitorType = type0
	useUllageSimulation = false
	IGNITOR_RESOURCE
	{
		name = LiquidFuel
		amount = 1.8
	}
	IGNITOR_RESOURCE
	{
		name = Oxidizer
		amount = 2.2
	}
}

Here we have 4 times of ignitions.
If you want unlimited ignitions, use -1.

The autoIgnitionTemperature works as a threshold that decides whether the ignition can take place automatically when we pump fuel into the combustion chamber, without the need to consume a set of ignitor.
Temperature is in centigrade unit.

The ignitor type is type0, you can set any typename here except "universal". This will limit the capability of reloading when the types don't match.

The useUllageSimulation is an option with default value "true". The ullage simulation will simulate the fuel flow stability according to your vessel's acceleration and rotation and the fuel flow stability will affect whether ignition will be a success or not. It will also forcibly shutdown an engine even it's already ignited if your vessel has some extreme acceleration/rotation. (The plugin has implemented but disabled engine explosion feature which will happen in more extreme situations)

IGNITOR_RESOURCE nodes represent the requirement of resources when ignite. You can add multiple IGNITOR_RESOURCE nodes or not add at all. When you have insufficient resources the probability of successful ignition will drop, if any of these resources are out-of-stock, the ignition will fail.
	For hypergolic fuel engine, you can use the above example in which the ignitor resources are the same as the resources used in the burn.
	For spark-plug ignitor, obviously you can use ElectricCharge as the resource.
	For other non-hypergolic fuel engine, you can define your own ignitor resource(s) or use the stock MonoPropellant/XenonGas. In this package, a resource named "HypergolicFluid" is provided.
	These are just suggestions, you can choose whatever you want.

==================================

2) ModuleExternalIgnitor

Code to add to part.cfg of the external ignitor:

MODULE
{
	name = ModuleExternalIgnitor
	ignitorType = universal
	igniteRange = 3.0
	ignitionsAvailable = -1
	provideRequiredResources = true
	IGNITOR_RESOURCE
	{
		name = ElectricCharge
		amount = 20.0
	}
}

Here we have the ignitorType of "universal" which means it can ignite all types of engines.
You can set any typename as you want. Remember to match with engine's ignitorType.

You can set how far can the ignitor ignite the engine. The unit is meter.

You can set the ignitions count for external ignitors too, just like the ModuleEngineIgnitor.

You can have ignition resource requirements (accept more than one resources) for external ignitors too, NOTE that this only works when provideRequiredResources = true and the required resources must be within the external ignitor part itself.

You can choose whether the external ignitor can provide the resources (if required) during the ignition.

==================================

3) EngineIgnitorUnit

Code to add to part.cfg of the reload pack:

MODULE
{
	name = EngineIgnitorUnit
	ignitors = 8
	ignitorType = universal
}

The ignitors represent how many sets of ignitor remained in this pack.

Here we have the ignitorType of "universal" which means it can reload all types of engines.

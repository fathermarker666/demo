// Copyright 2021, Infima Games. All Rights Reserved.

using System;
using UnityEngine;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace InfimaGames.LowPolyShooterPack
{
	/// <summary>
	/// Main Character Component. This component handles the most important functions of the character, and interfaces
	/// with basically every part of the asset, it is the hub where it all converges.
	/// </summary>
	[RequireComponent(typeof(CharacterKinematics))]
	public sealed class Character : CharacterBehaviour
	{
		#region FIELDS SERIALIZED

		[Header("Inventory")]
		
		[Tooltip("Inventory.")]
		[SerializeField]
		private InventoryBehaviour inventory;

		[Header("Cameras")]

		[Tooltip("Normal Camera.")]
		[SerializeField]
		private Camera cameraWorld;

		[Header("Animation")]

		[Tooltip("Determines how smooth the locomotion blendspace is.")]
		[SerializeField]
		private float dampTimeLocomotion = 0.15f;

		[Tooltip("How smoothly we play aiming transitions. Beware that this affects lots of things!")]
		[SerializeField]
		private float dampTimeAiming = 0.3f;
		
		[Header("Animation Procedural")]
		
		[Tooltip("Character Animator.")]
		[SerializeField]
		private Animator characterAnimator;

		#endregion

		#region FIELDS

		/// <summary>
		/// True if the character is aiming.
		/// </summary>
		private bool aiming;
		/// <summary>
		/// True if the character is running.
		/// </summary>
		private bool running;
		/// <summary>
		/// True if the character has its weapon holstered.
		/// </summary>
		private bool holstered;
		
		/// <summary>
		/// Last Time.time at which we shot.
		/// </summary>
		private float lastShotTime;
		
		/// <summary>
		/// Overlay Layer Index. Useful for playing things like firing animations.
		/// </summary>
		private int layerOverlay;
		/// <summary>
		/// Holster Layer Index. Used to play holster animations.
		/// </summary>
		private int layerHolster;
		/// <summary>
		/// Actions Layer Index. Used to play actions like reloading.
		/// </summary>
		private int layerActions;

		/// <summary>
		/// Character Kinematics. Handles all the IK stuff.
		/// </summary>
		private CharacterKinematics characterKinematics;
		
		/// <summary>
		/// The currently equipped weapon.
		/// </summary>
		private WeaponBehaviour equippedWeapon;
		/// <summary>
		/// The equipped weapon's attachment manager.
		/// </summary>
		private WeaponAttachmentManagerBehaviour weaponAttachmentManager;
		
		/// <summary>
		/// The scope equipped on the character's weapon.
		/// </summary>
		private ScopeBehaviour equippedWeaponScope;
		/// <summary>
		/// The magazine equipped on the character's weapon.
		/// </summary>
		private MagazineBehaviour equippedWeaponMagazine;
		
		/// <summary>
		/// True if the character is reloading.
		/// </summary>
		private bool reloading;
		
		/// <summary>
		/// True if the character is inspecting its weapon.
		/// </summary>
		private bool inspecting;

		/// <summary>
		/// True if the character is in the middle of holstering a weapon.
		/// </summary>
		private bool holstering;

		/// <summary>
		/// Look Axis Values.
		/// </summary>
		private Vector2 axisLook;
		/// <summary>
		/// Some non-XInput gamepads report the right stick with swapped axes.
		/// Keep this compatibility fix local to gamepad look input so mouse look stays untouched.
		/// </summary>
		[SerializeField]
		private bool swapNonXInputLookAxes = true;
		/// <summary>
		/// Cheap generic controllers often expose the right stick as Z/Rz joystick axes instead of a gamepad right stick.
		/// Read those directly so we can keep mouse and standard gamepad input unchanged.
		/// </summary>
		[SerializeField]
		private bool useJoystickZRLookAxes = true;
		[SerializeField]
		private float joystickZRLookScale = 1.2f;
		[SerializeField]
		private float joystickZRLookDeadzone = 0.15f;
		/// <summary>
		/// Look Axis Values.
		/// </summary>
		private Vector2 axisMovement;
		private AxisControl joystickLookAxisZ;
		private AxisControl joystickLookAxisRz;
		
		/// <summary>
		/// True if the player is holding the aiming button.
		/// </summary>
		private bool holdingButtonAim;
		/// <summary>
		/// True if the player is holding the running button.
		/// </summary>
		private bool holdingButtonRun;
		/// <summary>
		/// True if the player is holding the firing button.
		/// </summary>
		private bool holdingButtonFire;

		/// <summary>
		/// If true, the tutorial text should be visible on screen.
		/// </summary>
		private bool tutorialTextVisible;

		/// <summary>
		/// True if the game cursor is locked! Used when pressing "Escape" to allow developers to more easily access the editor.
		/// </summary>
		private bool cursorLocked;

		private bool hasMovementAnimatorParameter;
		private bool hasAimingAnimatorParameter;
		private bool hasAimAnimatorParameter;
		private bool hasRunningAnimatorParameter;
		private bool hasHolsteredAnimatorParameter;

		#endregion

		#region CONSTANTS

		/// <summary>
		/// Aiming Alpha Value.
		/// </summary>
		private static readonly int HashAimingAlpha = Animator.StringToHash("Aiming");

		/// <summary>
		/// Hashed "Movement".
		/// </summary>
		private static readonly int HashMovement = Animator.StringToHash("Movement");

		private static readonly int HashAim = Animator.StringToHash("Aim");
		private static readonly int HashRunning = Animator.StringToHash("Running");
		private static readonly int HashHolstered = Animator.StringToHash("Holstered");

		#endregion

		#region UNITY

		protected override void Awake()
		{
			#region Lock Cursor

			//Always make sure that our cursor is locked when the game starts!
			cursorLocked = true;
			//Update the cursor's state.
			UpdateCursorState();

			#endregion

			//Cache the CharacterKinematics component.
			characterKinematics = GetComponent<CharacterKinematics>();

			//Initialize Inventory.
			inventory.Init();

			//Refresh!
			RefreshWeaponSetup();
		}
		protected override void Start()
		{
			CacheAnimatorCompatibility();
		}

		protected override void Update()
		{
			ApplyJoystickLookAxes();
			
			// Bullfight mode uses a separate hand/combat pipeline.
			// Keep shooter fire/aim inputs suppressed so trigger combos (ZL+ZR) never play gun effects.
			if (ShouldSuppressWeaponActions())
			{
				holdingButtonAim = false;
				holdingButtonFire = false;
			}

			//Match Aim.
			aiming = holdingButtonAim && CanAim();
			//Match Run.
			running = holdingButtonRun && CanRun();

			//Holding the firing button.
			if (holdingButtonFire)
			{
				//Check.
				if (CanPlayAnimationFire() && equippedWeapon.HasAmmunition() && equippedWeapon.IsAutomatic())
				{
					//Has fire rate passed.
					if (Time.time - lastShotTime > 60.0f / equippedWeapon.GetRateOfFire())
						Fire();
				}	
			}

			//Update Animator.
			UpdateAnimator();
		}

		protected override void LateUpdate()
		{
			//We need a weapon for this!
			if (equippedWeapon == null)
				return;

			//Weapons without a scope should not be a thing! Ironsights are a scope too!
			if (equippedWeaponScope == null)
				return;
			
			//Make sure that we have a kinematics component!
			if(characterKinematics != null)
			{
				//Compute.
				characterKinematics.Compute();
			}
		}
		
		#endregion

		#region GETTERS

		public override Camera GetCameraWorld() => cameraWorld;

		public override InventoryBehaviour GetInventory() => inventory;
		
		public override bool IsCrosshairVisible() => !aiming && !holstered;
		public override bool IsRunning() => running;
		
		public override bool IsAiming() => aiming;
		public override bool IsCursorLocked() => cursorLocked;
		
		public override bool IsTutorialTextVisible() => tutorialTextVisible;
		
		public override Vector2 GetInputMovement() => axisMovement;
		public override Vector2 GetInputLook() => axisLook;

		#endregion

		#region METHODS

		/// <summary>
		/// Updates all the animator properties for this frame.
		/// </summary>
		private void UpdateAnimator()
		{
			//Movement Value. This value affects absolute movement. Aiming movement uses this, as opposed to per-axis movement.
			if (hasMovementAnimatorParameter)
				characterAnimator.SetFloat(HashMovement, Mathf.Clamp01(Mathf.Abs(axisMovement.x) + Mathf.Abs(axisMovement.y)), dampTimeLocomotion, Time.deltaTime);
			
			//Update the aiming value, but use interpolation. This makes sure that things like firing can transition properly.
			if (hasAimingAnimatorParameter)
				characterAnimator.SetFloat(HashAimingAlpha, Convert.ToSingle(aiming), 0.25f / 1.0f * dampTimeAiming, Time.deltaTime);

			//Update Animator Aiming.
			if (hasAimAnimatorParameter)
				characterAnimator.SetBool(HashAim, aiming);
			
			//Update Animator Running.
			if (hasRunningAnimatorParameter)
				characterAnimator.SetBool(HashRunning, running);
		}
		
		/// <summary>
		/// Plays the inspect animation.
		/// </summary>
		private void Inspect()
		{
			//State.
			inspecting = true;
			//Play.
			characterAnimator.CrossFade("Inspect", 0.0f, layerActions, 0);
		}
		
		/// <summary>
		/// Fires the character's weapon.
		/// </summary>
		private void Fire()
		{
			//Save the shot time, so we can calculate the fire rate correctly.
			lastShotTime = Time.time;
			//Fire the weapon! Make sure that we also pass the scope's spread multiplier if we're aiming.
			equippedWeapon.Fire();

			//Play firing animation.
			const string stateName = "Fire";
			characterAnimator.CrossFade(stateName, 0.05f, layerOverlay, 0);
		}

		private void PlayReloadAnimation()
		{
			#region Animation

			//Get the name of the animation state to play, which depends on weapon settings, and ammunition!
			string stateName = equippedWeapon.HasAmmunition() ? "Reload" : "Reload Empty";
			//Play the animation state!
			characterAnimator.Play(stateName, layerActions, 0.0f);

			//Set.
			reloading = true;

			#endregion

			//Reload.
			equippedWeapon.Reload();
		}

		/// <summary>
		/// Equip Weapon Coroutine.
		/// </summary>
		private IEnumerator Equip(int index = 0)
		{
			//Only if we're not holstered, holster. If we are already, we don't need to wait.
			if(!holstered)
			{
				//Holster.
				SetHolstered(holstering = true);
				//Wait.
				yield return new WaitUntil(() => holstering == false);
			}
			//Unholster. We do this just in case we were holstered.
			SetHolstered(false);
			//Play Unholster Animation.
			characterAnimator.Play("Unholster", layerHolster, 0);
			
			//Equip The New Weapon.
			inventory.Equip(index);
			//Refresh.
			RefreshWeaponSetup();
		}

		/// <summary>
		/// Refresh all weapon things to make sure we're all set up!
		/// </summary>
		private void RefreshWeaponSetup()
		{
			//Make sure we have a weapon. We don't want errors!
			if ((equippedWeapon = inventory.GetEquipped()) == null)
				return;
			
			/*
			 * Bullfight mode drives first-person hands with a custom controller.
			 * In that mode we keep the existing animator controller instead of
			 * replacing it with the weapon pack controller during startup.
			 */
			//Update Animator Controller. We do this to update all animations to a specific weapon's set.
			if (!UsesExternalHandAnimator())
				characterAnimator.runtimeAnimatorController = equippedWeapon.GetAnimatorController();

			CacheAnimatorCompatibility();

			//Get the attachment manager so we can use it to get all the attachments!
			weaponAttachmentManager = equippedWeapon.GetAttachmentManager();
			if (weaponAttachmentManager == null) 
				return;
			
			//Get equipped scope. We need this one for its settings!
			equippedWeaponScope = weaponAttachmentManager.GetEquippedScope();
			//Get equipped magazine. We need this one for its settings!
			equippedWeaponMagazine = weaponAttachmentManager.GetEquippedMagazine();
		}

		private void FireEmpty()
		{
			/*
			 * Save Time. Even though we're not actually firing, we still need this for the fire rate between
			 * empty shots.
			 */
			lastShotTime = Time.time;
			//Play.
			characterAnimator.CrossFade("Fire Empty", 0.05f, layerOverlay, 0);
		}

		/// <summary>
		/// Updates the cursor state based on the value of the cursorLocked variable.
		/// </summary>
		private void UpdateCursorState()
		{
			//Update cursor visibility.
			Cursor.visible = !cursorLocked;
			//Update cursor lock state.
			Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
		}

		/// <summary>
		/// Updates the "Holstered" variable, along with the Character's Animator value.
		/// </summary>
		private void SetHolstered(bool value = true)
		{
			//Update value.
			holstered = value;
			
			//Update Animator.
			if (hasHolsteredAnimatorParameter)
				characterAnimator.SetBool(HashHolstered, holstered);	
		}

		private bool UsesExternalHandAnimator() =>
			GetComponent<PlayerStats>() != null ||
			GetComponent<BullfightHandAnimatorController>() != null;

		private bool ShouldSuppressWeaponActions() => UsesExternalHandAnimator();

		private void CacheAnimatorCompatibility()
		{
			layerHolster = -1;
			layerActions = -1;
			layerOverlay = -1;
			hasMovementAnimatorParameter = false;
			hasAimingAnimatorParameter = false;
			hasAimAnimatorParameter = false;
			hasRunningAnimatorParameter = false;
			hasHolsteredAnimatorParameter = false;

			if (characterAnimator == null)
				return;

			layerHolster = characterAnimator.GetLayerIndex("Layer Holster");
			layerActions = characterAnimator.GetLayerIndex("Layer Actions");
			layerOverlay = characterAnimator.GetLayerIndex("Layer Overlay");
			hasMovementAnimatorParameter = HasAnimatorParameter(HashMovement, AnimatorControllerParameterType.Float);
			hasAimingAnimatorParameter = HasAnimatorParameter(HashAimingAlpha, AnimatorControllerParameterType.Float);
			hasAimAnimatorParameter = HasAnimatorParameter(HashAim, AnimatorControllerParameterType.Bool);
			hasRunningAnimatorParameter = HasAnimatorParameter(HashRunning, AnimatorControllerParameterType.Bool);
			hasHolsteredAnimatorParameter = HasAnimatorParameter(HashHolstered, AnimatorControllerParameterType.Bool);
		}

		private bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType parameterType)
		{
			if (characterAnimator == null)
				return false;

			foreach (AnimatorControllerParameter parameter in characterAnimator.parameters)
			{
				if (parameter.type == parameterType && parameter.nameHash == parameterHash)
					return true;
			}

			return false;
		}
		
		#region ACTION CHECKS

		/// <summary>
		/// Can Fire.
		/// </summary>
		private bool CanPlayAnimationFire()
		{
			//Block.
			if (holstered || holstering)
				return false;

			//Block.
			if (reloading)
				return false;

			//Block.
			if (inspecting)
				return false;

			//Return.
			return true;
		}

		/// <summary>
		/// Determines if we can play the reload animation.
		/// </summary>
		private bool CanPlayAnimationReload()
		{
			//No reloading!
			if (reloading)
				return false;

			//Block while inspecting.
			if (inspecting)
				return false;
			
			//Return.
			return true;
		}

		/// <summary>
		/// Returns true if the character is able to holster their weapon.
		/// </summary>
		/// <returns></returns>
		private bool CanPlayAnimationHolster()
		{
			//Block.
			if (reloading)
				return false;

			//Block.
			if (inspecting)
				return false;
			
			//Return.
			return true;
		}

		/// <summary>
		/// Returns true if the Character can change their Weapon.
		/// </summary>
		/// <returns></returns>
		private bool CanChangeWeapon()
		{
			//Block.
			if (holstering)
				return false;

			//Block.
			if (reloading)
				return false;

			//Block.
			if (inspecting)
				return false;
			
			//Return.
			return true;
		}

		/// <summary>
		/// Returns true if the Character can play the Inspect animation.
		/// </summary>
		private bool CanPlayAnimationInspect()
		{
			//Block.
			if (holstered || holstering)
				return false;

			//Block.
			if (reloading)
				return false;

			//Block.
			if (inspecting)
				return false;
			
			//Return.
			return true;
		}

		/// <summary>
		/// Returns true if the Character can Aim.
		/// </summary>
		/// <returns></returns>
		private bool CanAim()
		{
			//Block.
			if (holstered || inspecting)
				return false;

			//Block.
			if (reloading || holstering)
				return false;
			
			//Return.
			return true;
		}
		
		/// <summary>
		/// Returns true if the character can run.
		/// </summary>
		/// <returns></returns>
		private bool CanRun()
		{
			//Block.
			if (inspecting)
				return false;

			//Block.
			if (reloading || aiming)
				return false;

			//While trying to fire, we don't want to run. We do this just in case we do fire.
			if (holdingButtonFire && equippedWeapon.HasAmmunition())
				return false;

			//This blocks running backwards, or while fully moving sideways.
			if (axisMovement.y <= 0 || Math.Abs(Mathf.Abs(axisMovement.x) - 1) < 0.01f)
				return false;
			
			//Return.
			return true;
		}

		#endregion

		#region INPUT

		/// <summary>
		/// Fire.
		/// </summary>
		public void OnTryFire(InputAction.CallbackContext context)
		{
			//Block while the cursor is unlocked.
			if (!cursorLocked)
				return;

			if (ShouldSuppressWeaponActions())
			{
				holdingButtonFire = false;
				return;
			}

			//Switch.
			switch (context)
			{
				//Started.
				case {phase: InputActionPhase.Started}:
					//Hold.
					holdingButtonFire = true;
					break;
				//Performed.
				case {phase: InputActionPhase.Performed}:
					//Ignore if we're not allowed to actually fire.
					if (!CanPlayAnimationFire())
						break;
					
					//Check.
					if (equippedWeapon.HasAmmunition())
					{
						//Check.
						if (equippedWeapon.IsAutomatic())
							break;
							
						//Has fire rate passed.
						if (Time.time - lastShotTime > 60.0f / equippedWeapon.GetRateOfFire())
							Fire();
					}
					//Fire Empty.
					else
						FireEmpty();
					break;
				//Canceled.
				case {phase: InputActionPhase.Canceled}:
					//Stop Hold.
					holdingButtonFire = false;
					break;
			}
		}
		/// <summary>
		/// Reload.
		/// </summary>
		public void OnTryPlayReload(InputAction.CallbackContext context)
		{
			//Block while the cursor is unlocked.
			if (!cursorLocked)
				return;
			
			//Block.
			if (!CanPlayAnimationReload())
				return;
			
			//Switch.
			switch (context)
			{
				//Performed.
				case {phase: InputActionPhase.Performed}:
					//Play Animation.
					PlayReloadAnimation();
					break;
			}
		}

		/// <summary>
		/// Inspect.
		/// </summary>
		public void OnTryInspect(InputAction.CallbackContext context)
		{
			//Block while the cursor is unlocked.
			if (!cursorLocked)
				return;
			
			//Block.
			if (!CanPlayAnimationInspect())
				return;
			
			//Switch.
			switch (context)
			{
				//Performed.
				case {phase: InputActionPhase.Performed}:
					//Play Animation.
					Inspect();
					break;
			}
		}
		/// <summary>
		/// Aiming.
		/// </summary>
		public void OnTryAiming(InputAction.CallbackContext context)
		{
			//Block while the cursor is unlocked.
			if (!cursorLocked)
				return;

			if (ShouldSuppressWeaponActions())
			{
				holdingButtonAim = false;
				return;
			}

			//Switch.
			switch (context.phase)
			{
				case InputActionPhase.Started:
					//Started.
					holdingButtonAim = true;
					break;
				case InputActionPhase.Canceled:
					//Canceled.
					holdingButtonAim = false;
					break;
			}
		}

		/// <summary>
		/// Holster.
		/// </summary>
		public void OnTryHolster(InputAction.CallbackContext context)
		{
			//Block while the cursor is unlocked.
			if (!cursorLocked)
				return;
			
			//Switch.
			switch (context.phase)
			{
				//Performed.
				case InputActionPhase.Performed:
					//Check.
					if (CanPlayAnimationHolster())
					{
						//Set.
						SetHolstered(!holstered);
						//Holstering.
						holstering = true;
					}
					break;
			}
		}
		/// <summary>
		/// Run. 
		/// </summary>
		public void OnTryRun(InputAction.CallbackContext context)
		{
			//Block while the cursor is unlocked.
			if (!cursorLocked)
				return;
			
			//Switch.
			switch (context.phase)
			{
				//Started.
				case InputActionPhase.Started:
					//Start.
					holdingButtonRun = true;
					break;
				//Canceled.
				case InputActionPhase.Canceled:
					//Stop.
					holdingButtonRun = false;
					break;
			}
		}
		/// <summary>
		/// Next Inventory Weapon.
		/// </summary>
		public void OnTryInventoryNext(InputAction.CallbackContext context)
		{
			//Block while the cursor is unlocked.
			if (!cursorLocked)
				return;
			
			//Null Check.
			if (inventory == null)
				return;
			
			//Switch.
			switch (context)
			{
				//Performed.
				case {phase: InputActionPhase.Performed}:
					//Get the index increment direction for our inventory using the scroll wheel direction. If we're not
					//actually using one, then just increment by one.
					float scrollValue = context.valueType.IsEquivalentTo(typeof(Vector2)) ? Mathf.Sign(context.ReadValue<Vector2>().y) : 1.0f;
					
					//Get the next index to switch to.
					int indexNext = scrollValue > 0 ? inventory.GetNextIndex() : inventory.GetLastIndex();
					//Get the current weapon's index.
					int indexCurrent = inventory.GetEquippedIndex();
					
					//Make sure we're allowed to change, and also that we're not using the same index, otherwise weird things happen!
					if (CanChangeWeapon() && (indexCurrent != indexNext))
						StartCoroutine(nameof(Equip), indexNext);
					break;
			}
		}
		
		public void OnLockCursor(InputAction.CallbackContext context)
		{
			//Switch.
			switch (context)
			{
				//Performed.
				case {phase: InputActionPhase.Performed}:
					//Toggle the cursor locked value.
					cursorLocked = !cursorLocked;
					//Update the cursor's state.
					UpdateCursorState();
					break;
			}
		}
		
		/// <summary>
		/// Movement.
		/// </summary>
		public void OnMove(InputAction.CallbackContext context)
		{
			//Read.
			axisMovement = cursorLocked ? context.ReadValue<Vector2>() : default;
		}
		/// <summary>
		/// Look.
		/// </summary>
		public void OnLook(InputAction.CallbackContext context)
		{
			//Read.
			Vector2 lookInput = cursorLocked ? context.ReadValue<Vector2>() : default;

			if (swapNonXInputLookAxes && ShouldSwapLookAxes(context))
				lookInput = new Vector2(lookInput.y, lookInput.x);

			axisLook = lookInput;
		}

		private static bool ShouldSwapLookAxes(InputAction.CallbackContext context)
		{
			InputControl control = context.control;
			if (control == null || control.device is not Gamepad)
				return false;

			return IsGenericLookQuirkDevice(control.device);
		}

		private static bool IsGenericLookQuirkDevice(InputDevice device)
		{
			if (device == null)
				return false;

			if (IsStandardLookLayout(device))
				return false;

			return device.layout == "Gamepad" || device.layout == "Joystick";
		}

		private static bool IsStandardLookLayout(InputDevice device)
		{
			if (device == null)
				return false;

			if (HasDeviceUsage(device, ArduinoTest.VirtualGamepadUsage))
				return true;

			if (device.layout == "XInputControllerWindows")
				return true;

			if (InputSystem.IsFirstLayoutBasedOnSecond(device.layout, "DualShockGamepad"))
				return true;

			string identity = $"{device.layout} {device.displayName} {device.description.product} {device.description.manufacturer} {device.GetType().Name}".ToLowerInvariant();

			return identity.Contains("dualshock") ||
			       identity.Contains("dualsense") ||
			       identity.Contains("playstation") ||
			       identity.Contains("wireless controller") ||
			       identity.Contains("sony") ||
			       identity.Contains("xinput") ||
			       identity.Contains("xbox");
		}

		private static bool HasDeviceUsage(InputDevice device, string usage)
		{
			if (device == null || string.IsNullOrWhiteSpace(usage))
				return false;

			for (int i = 0; i < device.usages.Count; i++)
			{
				if (string.Equals(device.usages[i].ToString(), usage, StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		private void ApplyJoystickLookAxes()
		{
			if (!cursorLocked || !useJoystickZRLookAxes)
				return;

			Gamepad gamepad = Gamepad.current;
			if (gamepad != null && IsStandardLookLayout(gamepad))
				return;

			Joystick joystick = Joystick.current;
			if (joystick == null || !IsGenericLookQuirkDevice(joystick))
			{
				joystickLookAxisZ = null;
				joystickLookAxisRz = null;
				return;
			}

			if (joystickLookAxisZ == null || joystickLookAxisZ.device != joystick)
				joystickLookAxisZ = joystick.TryGetChildControl<AxisControl>("z");

			if (joystickLookAxisRz == null || joystickLookAxisRz.device != joystick)
				joystickLookAxisRz = joystick.TryGetChildControl<AxisControl>("rz");

			if (joystickLookAxisZ == null && joystickLookAxisRz == null)
				return;

			float lookX = joystickLookAxisZ?.ReadValue() ?? 0f;
			float lookY = -(joystickLookAxisRz?.ReadValue() ?? 0f);

			if (Mathf.Abs(lookX) < joystickZRLookDeadzone)
				lookX = 0f;

			if (Mathf.Abs(lookY) < joystickZRLookDeadzone)
				lookY = 0f;

			if (Mathf.Approximately(lookX, 0f) && Mathf.Approximately(lookY, 0f))
				return;

			axisLook = new Vector2(lookX, lookY) * joystickZRLookScale;
		}

		/// <summary>
		/// Called in order to update the tutorial text value.
		/// </summary>
		public void OnUpdateTutorial(InputAction.CallbackContext context)
		{
			//Switch.
			tutorialTextVisible = context switch
			{
				//Started. Show the tutorial.
				{phase: InputActionPhase.Started} => true,
				//Canceled. Hide the tutorial.
				{phase: InputActionPhase.Canceled} => false,
				//Default.
				_ => tutorialTextVisible
			};
		}

		#endregion

		#region ANIMATION EVENTS

		public override void EjectCasing()
		{
			//Notify the weapon.
			if(equippedWeapon != null)
				equippedWeapon.EjectCasing();
		}
		public override void FillAmmunition(int amount)
		{
			//Notify the weapon to fill the ammunition by the amount.
			if(equippedWeapon != null)
				equippedWeapon.FillAmmunition(amount);
		}
		
		public override void SetActiveMagazine(int active)
		{
			//Set magazine gameObject active.
			equippedWeaponMagazine.gameObject.SetActive(active != 0);
		}
		
		public override void AnimationEndedReload()
		{
			//Stop reloading!
			reloading = false;
		}

		public override void AnimationEndedInspect()
		{
			//Stop Inspecting.
			inspecting = false;
		}
		public override void AnimationEndedHolster()
		{
			//Stop Holstering.
			holstering = false;
		}

		#endregion

		#endregion
	}
}

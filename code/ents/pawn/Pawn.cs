using Sandbox;
using System.ComponentModel;

namespace MyGame;

public partial class Pawn : AnimatedEntity
{ 
	[Net, Predicted]
	public Weapon ActiveWeapon { get; set; }

	[ClientInput]
	public Vector3 InputDirection { get; set; }
	
	[ClientInput]
	public Angles ViewAngles { get; set; }

	/// <summary>
	/// Position a player should be looking from in world space.
	/// </summary>
	[Browsable( false )]
	public Vector3 EyePosition
	{
		get => Transform.PointToWorld( EyeLocalPosition );
		set => EyeLocalPosition = Transform.PointToLocal( value );
	}

	/// <summary>
	/// Position a player should be looking from in local to the entity coordinates.
	/// </summary>
	[Net, Predicted, Browsable( false )]
	public Vector3 EyeLocalPosition { get; set; }

	/// <summary>
	/// Rotation of the entity's "eyes", i.e. rotation for the camera when this entity is used as the view entity.
	/// </summary>
	[Browsable( false )]
	public Rotation EyeRotation
	{
		get => Transform.RotationToWorld( EyeLocalRotation );
		set => EyeLocalRotation = Transform.RotationToLocal( value );
	}

	/// <summary>
	/// Rotation of the entity's "eyes", i.e. rotation for the camera when this entity is used as the view entity. In local to the entity coordinates.
	/// </summary>
	[Net, Predicted, Browsable( false )]
	public Rotation EyeLocalRotation { get; set; }

	public BBox Hull
	{
		get => new
		(
			new Vector3( -16, -16, 0 ),
			new Vector3( 16, 16, 64 )
		);
	}

	[BindComponent] public PawnController Controller { get; }
	[BindComponent] public PawnAnimator Animator { get; }

	public override Ray AimRay => new Ray( EyePosition, EyeRotation.Forward );

	/// <summary>
	/// Called when the entity is first created 
	/// </summary>
	public override void Spawn()
	{
		SetModel( "models/citizen/citizen.vmdl" );

		EnableDrawing = true;
		EnableHideInFirstPerson = true;
		EnableShadowInFirstPerson = true;
	}

	public void SetActiveWeapon( Weapon weapon )
	{
		ActiveWeapon?.OnHolster();
		ActiveWeapon = weapon;
		ActiveWeapon.OnEquip( this );
	}

	public void Respawn()
	{
		Components.Create<PawnController>();
		Components.Create<PawnAnimator>();

		SetActiveWeapon( new Pistol() );
	}

	public void DressFromClient( IClient cl )
	{
		var c = new ClothingContainer();
		c.LoadFromClient( cl );
		c.DressEntity( this );
	}

	public override void Simulate( IClient client )
	{
		SimulateRotation();
		Controller?.Simulate( client );
		Animator?.Simulate();
		ActiveWeapon?.Simulate( client );
		EyeLocalPosition = Vector3.Up * (64f * Scale);
	}

	public override void BuildInput()
	{
		InputDirection = Input.AnalogMove;

		if ( Input.StopProcessing )
			return;

		var look = Input.AnalogLook;

		if ( ViewAngles.pitch > 90f || ViewAngles.pitch < -90f )
		{
			look = look.WithYaw( look.yaw * -1f );
		}

		if ( Input.Down( "left" ) && Input.Down( "forward" ) && Input.Pressed( "jump" ) )
		{
			Rotation rotate20Degs = Rotation.FromAxis( Vector3.Up, 20f );
			Vector3 rotatedForward = AimRay.Forward * rotate20Degs;
			Ray wallrunRay = new Ray( AimRay.Position, rotatedForward );
			TraceResult tr = Trace.Ray( wallrunRay, 200 )
					.StaticOnly()
					.Run();

			DebugOverlay.Sphere( AimRay.Position, 2f, Color.Blue, duration: 10f );
			DebugOverlay.Line( EyePosition, tr.EndPosition, 10f, false );
			if ( tr.Hit )
			{
				DebugOverlay.Sphere( tr.EndPosition, 2f, Color.Red, duration: 10f );
				Log.Info( "Wallrun Left!!!" );
			}
		}
		if ( Input.Down( "right" ) && Input.Down( "forward" ) && Input.Pressed( "jump" ) )
		{
			Rotation rotate20Degs = Rotation.FromAxis( Vector3.Up, -20f );
			Vector3 rotatedForward = AimRay.Forward * rotate20Degs;
			Ray wallrunRay = new Ray( AimRay.Position, rotatedForward );
			TraceResult tr = Trace.Ray( wallrunRay, 200 )
					.StaticOnly()
					.Run();

			DebugOverlay.Sphere( AimRay.Position, 2f, Color.Blue, duration: 10f );
			DebugOverlay.Line( EyePosition, tr.EndPosition, 10f, false );
			if ( tr.Hit )
			{
				DebugOverlay.Sphere( tr.EndPosition, 2f, Color.Red, duration: 10f );
				Log.Info( "Wallrun Right!!!" );
			}
		}


		if ( Input.Pressed( "attack1" ) ) {
			Rotation rotate20Degs = Rotation.FromAxis( Vector3.Up, -20f );
			Vector3 rotatedForward = AimRay.Forward * rotate20Degs;
			Ray wallrunRay = new Ray(AimRay.Position, rotatedForward);
			TraceResult tr = Trace.Ray( wallrunRay, 200 )
					.StaticOnly()
					.Run();

			DebugOverlay.Sphere( AimRay.Position, 2f, Color.Red, duration: 10f );
			DebugOverlay.Line( EyePosition, tr.EndPosition, 10f,  false);
			if (tr.Hit)
			{
				DebugOverlay.Sphere( tr.EndPosition, 2f, Color.Red, duration: 10f );
			}
		}

		var viewAngles = ViewAngles;
		viewAngles += look;
		viewAngles.pitch = viewAngles.pitch.Clamp( -89f, 89f );
		viewAngles.roll = 0f;
		ViewAngles = viewAngles.Normal;
	}

	bool IsThirdPerson { get; set; } = false;

	public override void FrameSimulate( IClient cl )
	{
		SimulateRotation();

		Camera.Rotation = ViewAngles.ToRotation();
		Camera.FieldOfView = Screen.CreateVerticalFieldOfView( Game.Preferences.FieldOfView );

		if ( Input.Pressed( "view" ) )
		{
			IsThirdPerson = !IsThirdPerson;
		}

		if ( IsThirdPerson )
		{
			Vector3 targetPos;
			var pos = Position + Vector3.Up * 64;
			var rot = Camera.Rotation * Rotation.FromAxis( Vector3.Up, -16 );

			float distance = 80.0f * Scale;
			targetPos = pos + rot.Right * ((CollisionBounds.Mins.x + 50) * Scale);
			targetPos += rot.Forward * -distance;

			var tr = Trace.Ray( pos, targetPos )
				.WithAnyTags( "solid" )
				.Ignore( this )
				.Radius( 8 )
				.Run();
			
			Camera.FirstPersonViewer = null;
			Camera.Position = tr.EndPosition;
		}
		else
		{
			Camera.FirstPersonViewer = this;
			Camera.Position = EyePosition;
		}
	}

	[GameEvent.Client.Frame]
	protected virtual void OnFrame()
	{

	}

	public TraceResult TraceBBox( Vector3 start, Vector3 end, float liftFeet = 0.0f )
	{
		return TraceBBox( start, end, Hull.Mins, Hull.Maxs, liftFeet );
	}

	public TraceResult TraceBBox( Vector3 start, Vector3 end, Vector3 mins, Vector3 maxs, float liftFeet = 0.0f )
	{
		if ( liftFeet > 0 )
		{
			start += Vector3.Up * liftFeet;
			maxs = maxs.WithZ( maxs.z - liftFeet );
		}

		var tr = Trace.Ray( start, end )
					.Size( mins, maxs )
					.WithAnyTags( "solid", "playerclip", "passbullets" )
					.Ignore( this )
					.Run();

		return tr;
	}

	protected void SimulateRotation()
	{
		EyeRotation = ViewAngles.ToRotation();
		Rotation = ViewAngles.WithPitch( 0f ).ToRotation();
	}
}

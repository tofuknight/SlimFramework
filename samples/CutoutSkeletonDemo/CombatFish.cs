using Godot;
using SlimFramework.CutoutSkeleton;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>
/// 一条鱼的运动学状态机：游、靠近、出手、收回。倒下后不再出手。
/// 规则只在样例里，插件不知道生命、目标和攻击。
/// </summary>
internal sealed class CombatFish
{
    private const float AttackRange = 130f;
    private const float AcquireRange = 340f;
    private const float RecoverSeconds = 0.32f;
    // 速度线性降到 0，总位移约是峰值乘时长的一半，大约 40 像素。
    private const float ShoveSpeed = 460f;
    private const float ShoveDuration = 0.18f;

    private enum Mind
    {
        Wander,
        Approach,
        Attack,
        KnockedOut
    }

    private readonly string _preferredAttack;
    private Mind _mind = Mind.Wander;
    private CombatFish? _target;
    private Vector2 _velocity;
    private Vector2 _wanderPoint;
    private double _wanderTimer;
    private double _cooldown;
    private string? _clip;
    private bool _dealsDamage;
    private bool _hitDone;
    private bool _hitCommitted;
    private double _timer;
    private double _length;
    private float _fade = 1f;
    private bool _mirrored;
    private bool _bellyUp;
    private float _flash;
    private Vector2 _shoveDir;
    private float _shoveLeft;
    private readonly ShaderMaterial _flashMaterial;

    public CombatFish(CutoutRig rig, int bodyTier, string preferredAttack, float speed, string species)
    {
        Rig = rig;
        BodyTier = bodyTier;
        Species = species;
        _preferredAttack = preferredAttack;
        Speed = speed;
        _flashMaterial = FishFlash.CreateMaterial();
        foreach (var slot in rig.SlotIds)
            rig.GetSlotNode(slot).Material = _flashMaterial;
    }

    public CutoutRig Rig { get; }

    public int BodyTier { get; }

    public string Species { get; }

    public float Speed { get; }

    public int Hp { get; private set; } = 4;

    public bool Alive => _mind != Mind.KnockedOut;

    public bool DealsDamage => _mind == Mind.Attack && _dealsDamage;

    public string? ActiveClip => _mind == Mind.Attack ? _clip : null;

    public int FlashStarts { get; private set; }

    public float FlashAmount
    {
        get
        {
            var value = _flashMaterial.GetShaderParameter(FishFlash.Parameter);
            return value.VariantType == Variant.Type.Nil ? 0f : value.AsSingle();
        }
    }

    public Vector2 Position => Rig.Root.Position;

    public void Boot(FishArena arena)
    {
        _wanderPoint = arena.RandomPoint();
        _wanderTimer = 0.4 + arena.NextDouble() * 1.2;
        Face(Rig.Root.Rotation);
        _velocity = Vector2.Right.Rotated(Rig.Root.Rotation) * (Speed * 0.35f);
        Rig.Play(OpenOceanFish.SwimClip);
    }

    public void FaceForSmoke(float heading, bool bellyUp)
    {
        _bellyUp = bellyUp;
        Face(heading);
    }

    public void Step(double dt, FishArena arena)
    {
        DecayFlash(dt);
        ApplyShove(dt, arena);
        if (_mind == Mind.KnockedOut)
        {
            Sink(dt, arena);
            return;
        }

        switch (_mind)
        {
            case Mind.Wander:
                Wander(dt, arena);
                break;
            case Mind.Approach:
                Approach(dt, arena);
                break;
            case Mind.Attack:
                Attack(dt, arena);
                break;
        }
    }

    public void TakeHit(FishArena arena, Vector2 from)
    {
        if (!Alive)
            return;
        Hp--;
        StartFlash();
        var away = Position - from;
        if (away.LengthSquared() > 4f)
        {
            _shoveDir = away.Normalized();
            _shoveLeft = ShoveDuration;
        }
        if (Hp <= 0)
        {
            KnockOut();
            return;
        }

        if (_mind == Mind.Attack)
            return;
        BeginClip(arena, OpenOceanFish.RecoilClip, dealsDamage: false);
    }

    private void Wander(double dt, FishArena arena)
    {
        if (_cooldown > 0d)
            _cooldown -= dt;
        else if (TryAcquire(arena))
            return;

        _wanderTimer -= dt;
        if (_wanderTimer <= 0d || Position.DistanceSquaredTo(_wanderPoint) < 28f * 28f)
        {
            _wanderPoint = arena.RandomPoint();
            _wanderTimer = 1.4 + arena.NextDouble() * 2.0;
        }

        var to = _wanderPoint - Position;
        var desired = to.LengthSquared() < 1f ? _velocity : to.Normalized() * Speed;
        Steer(desired, dt, arena);
    }

    private void Approach(double dt, FishArena arena)
    {
        if (_target == null || !_target.Alive)
        {
            _target = null;
            _mind = Mind.Wander;
            return;
        }

        var to = _target.Position - Position;
        var dist = to.Length();
        if (dist > AcquireRange * 1.35f)
        {
            _target = null;
            _mind = Mind.Wander;
            return;
        }

        if (dist <= AttackRange)
        {
            BeginStrike(arena);
            return;
        }

        Steer(to / dist * Speed, dt, arena);
    }

    private void Attack(double dt, FishArena arena)
    {
        _timer += dt;
        var forward = Vector2.Right.Rotated(Rig.Root.Rotation);
        var surge = _clip == OpenOceanFish.LungeClip ? 160f : 24f;
        Rig.Root.Position = arena.Clamp(Position + forward * surge * (float)dt);

        if (_dealsDamage && !_hitDone && _timer >= _length * 0.45d)
        {
            _hitDone = true;
            TryHit(arena);
        }

        if (_timer >= _length)
            FinishClip();
    }

    private bool TryAcquire(FishArena arena)
    {
        var target = PickTarget(arena);
        if (target == null)
            return false;
        _target = target;
        if (Position.DistanceSquaredTo(target.Position) <= AttackRange * AttackRange)
            BeginStrike(arena);
        else
            _mind = Mind.Approach;
        return true;
    }

    private CombatFish? PickTarget(FishArena arena)
    {
        CombatFish? nearest = null;
        CombatFish? randomPick = null;
        var best = float.MaxValue;
        var seen = 0;
        foreach (var other in arena.Fish)
        {
            if (!other.Alive || ReferenceEquals(other, this))
                continue;
            var distSq = Position.DistanceSquaredTo(other.Position);
            if (distSq < best)
            {
                best = distSq;
                nearest = other;
            }

            if (distSq > AcquireRange * AcquireRange)
                continue;
            seen++;
            if (arena.NextInt(seen) == 0)
                randomPick = other;
        }

        if (nearest == null || randomPick == null)
            return null;
        if (best <= AttackRange * AttackRange)
            return arena.NextDouble() < 0.75 ? nearest : randomPick;
        return randomPick;
    }

    private void BeginStrike(FishArena arena)
    {
        if (_target == null || !_target.Alive)
        {
            _mind = Mind.Wander;
            return;
        }

        var to = _target.Position - Position;
        _hitCommitted = to.LengthSquared() <= AttackRange * AttackRange;
        var forward = Vector2.Right.Rotated(Rig.Root.Rotation);
        var behind = to.LengthSquared() > 1f && forward.Dot(to.Normalized()) < -0.2f;
        string clip;
        if (behind)
            clip = OpenOceanFish.TailSwipeClip;
        else
        {
            clip = arena.NextDouble() < 0.8 ? _preferredAttack : arena.RandomAttack();
            if (to.LengthSquared() > 1f)
                Face(to.Angle());
        }

        BeginClip(arena, clip, dealsDamage: true);
    }

    private void BeginClip(FishArena arena, string clip, bool dealsDamage)
    {
        _mind = Mind.Attack;
        _clip = clip;
        _dealsDamage = dealsDamage;
        _hitDone = false;
        _timer = 0d;
        _length = arena.Clips.Get(clip).Length;
        Rig.SetSpineSineEnabled(!OpenOceanFish.SuspendsSpineSine(clip));
        Rig.Play(clip);
        if (dealsDamage)
            arena.NotifyAttack(clip);
    }

    private void TryHit(FishArena arena)
    {
        if (!_hitCommitted || _target == null || !_target.Alive)
            return;
        if (Position.DistanceSquaredTo(_target.Position) > AttackRange * AttackRange * 4f)
            return;
        _target.TakeHit(arena, Position);
    }

    private void FinishClip()
    {
        _mind = Mind.Wander;
        _clip = null;
        _dealsDamage = false;
        _cooldown = RecoverSeconds;
        Rig.SetSpineSineEnabled(true);
        Rig.Play(OpenOceanFish.SwimClip);
    }

    private void KnockOut()
    {
        _mind = Mind.KnockedOut;
        Hp = 0;
        _target = null;
        _clip = null;
        _dealsDamage = false;
        Rig.SetSpineSineEnabled(false);
        Rig.Player.Stop();
        _bellyUp = true;
        Face(Rig.Root.Rotation);
    }

    private void Sink(double dt, FishArena arena)
    {
        var pos = Position;
        pos.Y = Mathf.Min(pos.Y + 48f * (float)dt, arena.Bounds.End.Y - 22f);
        Rig.Root.Position = pos;
        _fade = Mathf.MoveToward(_fade, 0.28f, (float)dt * 0.6f);
        Rig.Root.Modulate = new Color(0.72f, 0.8f, 0.88f, _fade);
    }

    private void Steer(Vector2 desired, double dt, FishArena arena)
    {
        var wish = desired + arena.Separation(this) + arena.WallPush(Position);
        var max = Speed;
        if (wish.LengthSquared() > max * max)
            wish = wish.Normalized() * max;
        var blend = 1f - Mathf.Exp(-4.8f * (float)dt);
        _velocity = _velocity.Lerp(wish, blend);
        Rig.Root.Position = arena.Clamp(Position + _velocity * (float)dt);
        if (_velocity.LengthSquared() > 36f)
            Face(_velocity.Angle());
    }

    private void Face(float heading) => FishFacing.Apply(Rig, heading, _bellyUp, ref _mirrored);

    internal void PrepareFadeForSmoke()
    {
        if (Alive)
            KnockOut();
        _fade = 1f;
        Rig.Root.Modulate = Colors.White;
    }

    private void StartFlash()
    {
        _flash = FishFlash.Duration;
        FlashStarts++;
        _flashMaterial.SetShaderParameter(FishFlash.Parameter, 1f);
    }

    private void DecayFlash(double dt)
    {
        if (_flash <= 0f)
            return;
        _flash = Mathf.Max(0f, _flash - (float)dt);
        var amount = _flash > FishFlash.Fade ? 1f : _flash / FishFlash.Fade;
        _flashMaterial.SetShaderParameter(FishFlash.Parameter, amount);
    }

    private void ApplyShove(double dt, FishArena arena)
    {
        if (_shoveLeft <= 0f)
            return;
        var step = Mathf.Min((float)dt, _shoveLeft);
        var speed = ShoveSpeed * (_shoveLeft / ShoveDuration);
        Rig.Root.Position = arena.Clamp(Position + _shoveDir * speed * step);
        _shoveLeft -= step;
    }
}

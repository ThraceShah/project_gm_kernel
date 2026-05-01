using ProjectGmKernel.Native.Generated;

namespace ProjectGmKernel.Native.Runtime;

internal enum EntityClass : int
{
    Session = 504,
    Point = 2501,
}

internal enum PoolKind : byte
{
    None = 0,
    Point = 1,
}

internal struct HandleRecord
{
    public EntityClass Class;
    public PoolKind Pool;
    public int SlotIndex;
    public int Generation;
    public int SessionId;
    public byte Alive;
}

internal struct PointRecord
{
    public int Generation;
    public byte Alive;
    public PK_VECTOR_s Position;
}

internal static unsafe class KernelRuntime
{
    private const int DefaultSessionId = 1;
    private const int MaxHandles = 4096;
    private const int MaxPoints = 2048;

    private static readonly System.Threading.Lock RuntimeLock = new();
    private static readonly SessionDispatchState DispatchState = new();
    private static readonly HandleRecord[] Handles = new HandleRecord[MaxHandles];
    private static readonly PointRecord[] Points = new PointRecord[MaxPoints];

    private static int nextTag = 1;
    private static int nextPointSlot;
    private static bool started;

    public static int SessionStart(PK_SESSION_start_o_s* options)
    {
        if (options is null)
        {
            return ParasolidConstants.PK_ERROR_bad_field_number;
        }

        if (options->o_t_version != 1)
        {
            return ParasolidConstants.PK_ERROR_o_t_version_incorrect;
        }

        using var scope = RuntimeLock.EnterScope();
        if (started)
        {
            return ParasolidConstants.PK_ERROR_rollback_started;
        }

        started = true;
        nextTag = 1;
        nextPointSlot = 0;
        Array.Clear(Handles);
        Array.Clear(Points);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int SessionStop()
    {
        using var scope = RuntimeLock.EnterScope();
        if (!started)
        {
            return ParasolidConstants.PK_ERROR_not_in_PK;
        }

        started = false;
        Array.Clear(Handles);
        Array.Clear(Points);
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int PointCreate(PK_POINT_sf_s* pointSf, int* pointTag)
    {
        if (pointSf is null || pointTag is null)
        {
            return ParasolidConstants.PK_ERROR_bad_field_number;
        }

        using var scope = RuntimeLock.EnterScope();
        if (!started)
        {
            return ParasolidConstants.PK_ERROR_not_in_PK;
        }

        if (nextPointSlot >= MaxPoints || nextTag >= MaxHandles)
        {
            return ParasolidConstants.PK_ERROR_general_body;
        }

        var pointSlot = nextPointSlot++;
        ref var point = ref Points[pointSlot];
        point.Generation++;
        point.Alive = 1;
        point.Position = pointSf->position;

        var tag = nextTag++;
        Handles[tag] = new HandleRecord
        {
            Alive = 1,
            Class = EntityClass.Point,
            Pool = PoolKind.Point,
            SlotIndex = pointSlot,
            Generation = point.Generation,
            SessionId = DefaultSessionId,
        };

        *pointTag = tag;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int EntityAskClass(int entityTag, int* classCode)
    {
        if (classCode is null)
        {
            return ParasolidConstants.PK_ERROR_bad_field_number;
        }

        using var scope = RuntimeLock.EnterScope();
        if (!started)
        {
            return ParasolidConstants.PK_ERROR_not_in_PK;
        }

        if (entityTag <= 0 || entityTag >= nextTag)
        {
            return ParasolidConstants.PK_ERROR_unknown_class;
        }

        var handle = Handles[entityTag];
        if (handle.Alive == 0 || handle.SessionId != DefaultSessionId)
        {
            return ParasolidConstants.PK_ERROR_unknown_class;
        }

        *classCode = (int)handle.Class;
        return ParasolidConstants.PK_ERROR_no_errors;
    }

    public static int Dispatch(ApiId apiId, ConcurrencyKind concurrencyKind, AccessKind accessKind, Func<int> action)
    {
        var descriptor = new CommandDescriptor
        {
            ApiId = apiId,
            ConcurrencyKind = concurrencyKind,
            AccessKind = accessKind,
            SessionId = DefaultSessionId,
        };

        return DispatchState.Execute(ref descriptor, action);
    }
}

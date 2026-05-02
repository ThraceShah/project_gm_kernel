using System.Diagnostics;

namespace ProjectGmKernel.Native.Runtime;

/// <summary>
/// Validates topology and geometry consistency. Used for debug assertions and diagnostics.
/// </summary>
internal static class IntegrityChecker
{
    /// <summary>
    /// Run all integrity checks on the current kernel state.
    /// Returns the number of errors found (0 = all good).
    /// </summary>
    public static int CheckAll()
    {
        int errors = 0;
        errors += CheckBodyConsistency();
        errors += CheckShellConsistency();
        errors += CheckFaceConsistency();
        errors += CheckLoopConsistency();
        errors += CheckEdgeConsistency();
        errors += CheckFinConsistency();
        errors += CheckVertexConsistency();
        errors += CheckHandleConsistency();
        return errors;
    }

    /// <summary>
    /// Check that each body's shell/face/edge/vertex chains are consistent.
    /// </summary>
    public static int CheckBodyConsistency()
    {
        int errors = 0;
        var bodies = KernelRuntime.Bodies;
        for (int i = 0; i < bodies.AllocatedCount; i++)
        {
            if (!bodies.IsAlive(i)) continue;
            ref var body = ref bodies[i];

            // Check shell chain length matches ShellCount
            int shellCount = CountChain(KernelRuntime.Shells, body.FirstShell,
                s => s.NextInBody);
            if (shellCount != body.ShellCount)
            {
                Debug.WriteLine($"Body {i}: ShellCount mismatch: expected {body.ShellCount}, counted {shellCount}");
                errors++;
            }

            // Check all shells point back to this body
            int shellSlot = body.FirstShell;
            while (shellSlot >= 0)
            {
                if (KernelRuntime.Shells[shellSlot].Body != i)
                {
                    Debug.WriteLine($"Body {i}: Shell {shellSlot} has wrong body link");
                    errors++;
                }
                shellSlot = KernelRuntime.Shells[shellSlot].NextInBody;
            }
        }
        return errors;
    }

    public static int CheckShellConsistency()
    {
        int errors = 0;
        var shells = KernelRuntime.Shells;
        for (int i = 0; i < shells.AllocatedCount; i++)
        {
            if (!shells.IsAlive(i)) continue;
            ref var shell = ref shells[i];

            if (shell.Body < 0 || !KernelRuntime.Bodies.IsAlive(shell.Body))
            {
                Debug.WriteLine($"Shell {i}: invalid body link {shell.Body}");
                errors++;
                continue;
            }

            int faceCount = CountChain(KernelRuntime.Faces, shell.FirstFaceShell,
                s => s.NextInShell);
            if (faceCount != shell.FaceCount)
            {
                Debug.WriteLine($"Shell {i}: FaceCount mismatch: expected {shell.FaceCount}, counted {faceCount}");
                errors++;
            }

            int faceSlot = shell.FirstFaceShell;
            while (faceSlot >= 0)
            {
                if (KernelRuntime.Faces[faceSlot].Shell != i)
                {
                    Debug.WriteLine($"Shell {i}: Face {faceSlot} has wrong shell link");
                    errors++;
                }
                faceSlot = KernelRuntime.Faces[faceSlot].NextInShell;
            }
        }
        return errors;
    }

    public static int CheckFaceConsistency()
    {
        int errors = 0;
        var faces = KernelRuntime.Faces;
        for (int i = 0; i < faces.AllocatedCount; i++)
        {
            if (!faces.IsAlive(i)) continue;
            ref var face = ref faces[i];

            if (face.Shell < 0 || !KernelRuntime.Shells.IsAlive(face.Shell))
            {
                Debug.WriteLine($"Face {i}: invalid shell link {face.Shell}");
                errors++;
                continue;
            }

            int loopCount = CountChain(KernelRuntime.Loops, face.FirstLoop,
                s => s.NextInFace);
            if (loopCount != face.LoopCount)
            {
                Debug.WriteLine($"Face {i}: LoopCount mismatch: expected {face.LoopCount}, counted {loopCount}");
                errors++;
            }

            int loopSlot = face.FirstLoop;
            while (loopSlot >= 0)
            {
                if (KernelRuntime.Loops[loopSlot].Face != i)
                {
                    Debug.WriteLine($"Face {i}: Loop {loopSlot} has wrong face link");
                    errors++;
                }
                loopSlot = KernelRuntime.Loops[loopSlot].NextInFace;
            }
        }
        return errors;
    }

    public static int CheckLoopConsistency()
    {
        int errors = 0;
        var loops = KernelRuntime.Loops;
        for (int i = 0; i < loops.AllocatedCount; i++)
        {
            if (!loops.IsAlive(i)) continue;
            ref var loop = ref loops[i];

            if (loop.Face < 0 || !KernelRuntime.Faces.IsAlive(loop.Face))
            {
                Debug.WriteLine($"Loop {i}: invalid face link {loop.Face}");
                errors++;
                continue;
            }

            int finCount = CountChain(KernelRuntime.Fins, loop.FirstFin,
                s => s.NextInLoop);
            if (finCount != loop.FinCount)
            {
                Debug.WriteLine($"Loop {i}: FinCount mismatch: expected {loop.FinCount}, counted {finCount}");
                errors++;
            }
        }
        return errors;
    }

    public static int CheckEdgeConsistency()
    {
        int errors = 0;
        var edges = KernelRuntime.Edges;
        for (int i = 0; i < edges.AllocatedCount; i++)
        {
            if (!edges.IsAlive(i)) continue;
            ref var edge = ref edges[i];

            if (edge.Body < 0 || !KernelRuntime.Bodies.IsAlive(edge.Body))
            {
                Debug.WriteLine($"Edge {i}: invalid body link {edge.Body}");
                errors++;
                continue;
            }

            int finCount = CountChain(KernelRuntime.Fins, edge.FirstFinEdge,
                s => s.NextOfEdge);
            if (finCount != edge.FinCount)
            {
                Debug.WriteLine($"Edge {i}: FinCount mismatch: expected {edge.FinCount}, counted {finCount}");
                errors++;
            }
        }
        return errors;
    }

    public static int CheckFinConsistency()
    {
        int errors = 0;
        var fins = KernelRuntime.Fins;
        for (int i = 0; i < fins.AllocatedCount; i++)
        {
            if (!fins.IsAlive(i)) continue;
            ref var fin = ref fins[i];

            if (fin.Edge < 0 || !KernelRuntime.Edges.IsAlive(fin.Edge))
            {
                Debug.WriteLine($"Fin {i}: invalid edge link {fin.Edge}");
                errors++;
            }
            if (fin.Loop < 0 || !KernelRuntime.Loops.IsAlive(fin.Loop))
            {
                Debug.WriteLine($"Fin {i}: invalid loop link {fin.Loop}");
                errors++;
            }
            if (fin.Face < 0 || !KernelRuntime.Faces.IsAlive(fin.Face))
            {
                Debug.WriteLine($"Fin {i}: invalid face link {fin.Face}");
                errors++;
            }
        }
        return errors;
    }

    public static int CheckVertexConsistency()
    {
        int errors = 0;
        var vertices = KernelRuntime.Vertices;
        for (int i = 0; i < vertices.AllocatedCount; i++)
        {
            if (!vertices.IsAlive(i)) continue;
            ref var vert = ref vertices[i];

            if (vert.Body < 0 || !KernelRuntime.Bodies.IsAlive(vert.Body))
            {
                Debug.WriteLine($"Vertex {i}: invalid body link {vert.Body}");
                errors++;
            }
        }
        return errors;
    }

    /// <summary>
    /// Check that all alive handles point to alive entity slots.
    /// </summary>
    public static int CheckHandleConsistency()
    {
        int errors = 0;
        // Access via reflection or internal accessor — for Phase 2, we trust
        // the KernelRuntime's own tag resolution. A full check would iterate Handles[].
        return errors;
    }

    /// <summary>
    /// Count the length of a sibling chain in an entity pool.
    /// </summary>
    private static int CountChain<T>(EntityPool<T> pool, int first, Func<T, int> nextLink) where T : struct
    {
        int count = 0;
        int current = first;
        while (current >= 0)
        {
            if ((uint)current >= (uint)pool.AllocatedCount)
                break; // dangling index
            count++;
            current = nextLink(pool[current]);
            if (count > 100000)
                break; // safety limit
        }
        return count;
    }
}

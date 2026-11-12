#!/usr/bin/env dotnet run
#:project ../third_party/PKToy/PskernelSharp/PskernelSharp.csproj
#:project ../src/ProjectGmKernel.Native/ProjectGmKernel.Native.csproj
#:property UsePskernelSharpUsings=true
#:property UseParasolidScriptHost=true
#:property UseParasolidMarkOracle=true
#:property AllowUnsafeBlocks=true
#:property AssemblyName=CurvedSheetPartitionOracle
using static parasolid;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using ProjectGmKernel.Native.Runtime;
using M=ProjectGmKernel.Native.Generated;

var sharedFree=args.Contains("--shared-free-sp",StringComparer.Ordinal);var name=args.Single(a=>!a.StartsWith("--",StringComparison.Ordinal));if(name.Length==0||name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not ('-' or '_')))throw new ArgumentException("new output name");
var root=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Source())!,".."));var output=Path.Combine(root,"temp_docs","native-surface-domain-oracle",name);
if(Directory.Exists(output))throw new InvalidOperationException("Immutable output");Directory.CreateDirectory(output);
Console.WriteLine("GM_ASSEMBLY sha256="+Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(KernelRuntime).Assembly.Location))));
if(!ParasolidScriptHost.TryStartSession("Native finite surface domain contract",out var host,out var reason,configureRollback:args.Contains("--mark",StringComparer.Ordinal)?MarkOracleStorage.Register:null))throw new Exception(reason);
using(host)unsafe
{
    PK_SESSION_kernel_version_t version;Check(PK_SESSION_ask_kernel_version(&version),"native version");Console.WriteLine($"PK_VERSION {version.major_revision}.{version.minor_revision}.{version.build_number}");
    var start=new M.PK_SESSION_start_o_s{o_t_version=1};Check(KernelRuntime.SessionStart(&start),"managed start");
    try
    {
        if(args.Contains("--blend-owner",StringComparer.Ordinal)) { BlendOwnership(sharedFree,args.Contains("--attached-support",StringComparer.Ordinal),args.Contains("--mark",StringComparer.Ordinal),args.Contains("--free-root-delete",StringComparer.Ordinal)); return; }
        if(args.Contains("--closed-solid",StringComparer.Ordinal)){ClosedSolid(output);return;}
        var inspect=args.Where(a=>a.StartsWith("--inspect-native=",StringComparison.Ordinal)).Select(a=>a[17..]).ToArray();
        if(inspect.Length!=0){InspectNativeDomains(root,output,inspect);return;}
        var mismatches=0;var ownershipDifferences=0;var runs=0;double* poles=stackalloc double[36];double* uk=stackalloc double[2];double* vk=stackalloc double[2];int* mult=stackalloc int[2]{3,3};
        double* uvPoles=stackalloc double[4];double* uvKnots=stackalloc double[2]{0,1};int* uvMult=stackalloc int[2]{2,2};
        Console.WriteLine("SOURCE_OWNERSHIP_MODE "+(sharedFree?"surface-has-free-SP-owner":"independent-orphan-surface"));
        foreach(var rational in new[]{false,true})foreach(var shifted in new[]{false,true})
        {
            uk[0]=shifted?2:0;uk[1]=shifted?5:1;vk[0]=shifted?-3:0;vk[1]=shifted?-1:1;var dimension=rational?4:3;
            for(var i=0;i<3;i++)for(var j=0;j<3;j++){var w=rational?1+.25*i+.5*j:1;var at=dimension*(3*i+j);poles[at]=i*.5*w;poles[at+1]=j*.5*w;poles[at+2]=(i==2&&j==2?.15:0)*w;if(rational)poles[at+3]=w;}
            var sf=new PK_BSURF_sf_t{u_degree=2,v_degree=2,n_u_vertices=3,n_v_vertices=3,vertex_dim=dimension,is_rational=rational?PK_LOGICAL_true:PK_LOGICAL_false,vertex=poles,
                n_u_knots=2,n_v_knots=2,u_knot=uk,v_knot=vk,u_knot_mult=mult,v_knot_mult=mult,form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};
            foreach(var (label,a,b,c,d) in new[]{("whole",0.0,1.0,0.0,1.0),("interior",.2,.8,.2,.8),("lower-outside",-.25,.25,.2,.8),("upper-outside",.75,1.25,.2,.8),("fully-outside",1.25,1.75,.2,.8),("all-edges",-.25,1.25,-.25,1.25)})
            {
                var stem=$"{(rational?"rational":"plain")}-{(shifted?"shifted":"unit")}-{label}";PK_BSURF_t referenceSource,source;
                Check(PK_BSURF_create(&sf,&referenceSource),"native definition");Check(KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&source),"managed definition");
                PK_BCURVE_t nativeUv=0,ownUv=0;PK_SPCURVE_t nativeSp=0,ownSp=0;
                if(sharedFree)
                {
                    uvPoles[0]=uk[0]+.2*(uk[1]-uk[0]);uvPoles[1]=vk[0]+.2*(vk[1]-vk[0]);uvPoles[2]=uk[0]+.8*(uk[1]-uk[0]);uvPoles[3]=vk[0]+.8*(vk[1]-vk[0]);
                    var cf=new PK_BCURVE_sf_t{degree=1,n_vertices=2,vertex_dim=2,vertex=uvPoles,n_knots=2,knot=uvKnots,knot_mult=uvMult,form=PK_BCURVE_form_arbitrary_c,knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c};
                    Check(PK_BCURVE_create(&cf,&nativeUv),"native free UV definition");Check(KernelRuntime.BCurveCreate((M.PK_BCURVE_sf_s*)&cf,&ownUv),"managed free UV definition");
                    var sp=new PK_SPCURVE_sf_t(referenceSource,nativeUv);Check(PK_SPCURVE_create(&sp,&nativeSp),"native free SP owner");sp=new(source,ownUv);Check(KernelRuntime.SpCurveCreate((M.PK_SPCURVE_sf_s*)&sp,&ownSp),"managed free SP owner");
                }
                var box=new PK_UVBOX_t();box.param[0]=uk[0]+a*(uk[1]-uk[0]);box.param[1]=vk[0]+c*(vk[1]-vk[0]);box.param[2]=uk[0]+b*(uk[1]-uk[0]);box.param[3]=vk[0]+d*(vk[1]-vk[0]);
                PK_BODY_t reference=0,actual=0;var pe=PK_SURF_make_sheet_body(referenceSource,box,&reference);var ge=KernelRuntime.SurfMakeSheetBody(source,*(M.PK_UVBOX_s*)&box,&actual);runs++;
                Console.WriteLine($"DOMAIN_OPERATION {stem} uv={box.param[0]:R}/{box.param[1]:R}/{box.param[2]:R}/{box.param[3]:R} native_error={pe} managed_error={ge}");
                if(pe!=ge)mismatches++;
                if(ge==0)File.WriteAllBytes(Path.Combine(output,stem+"-gm.x_t"),Transmit(true,actual));
                if(pe==0)
                {
                    File.WriteAllBytes(Path.Combine(output,stem+"-pk.x_t"),Transmit(false,reference));CheckBody(reference,stem+" independent");
                    int nf;int* faces;Check(PK_BODY_ask_faces(reference,&nf,&faces),"native faces");
                    try{for(var f=0;f<nf;f++){int support;Check(PK_FACE_ask_surf(faces[f],&support),"native face support");PK_UVBOX_t supportBox,faceBox;Check(PK_SURF_ask_uvbox(support,&supportBox),"native support domain");Check(PK_FACE_find_uvbox(faces[f],&faceBox),"native finite face domain");Console.WriteLine($"DOMAIN_RESULT {stem} reuses_original={support==referenceSource} support={supportBox.param[0]:R}/{supportBox.param[1]:R}/{supportBox.param[2]:R}/{supportBox.param[3]:R} face={faceBox.param[0]:R}/{faceBox.param[1]:R}/{faceBox.param[2]:R}/{faceBox.param[3]:R}");}}
                    finally{PK_MEMORY_free(faces);}
                }
                if(pe==0&&ge==0)
                {
                    var fresh=Receive(Transmit(true,actual));CheckBody(fresh,stem+" fresh GM");ParasolidBodyCompare.AssertEquivalent(reference,fresh,stem);Check(PK_ENTITY_delete(1,&fresh),"delete fresh");
                    int secondReference=0,secondActual=0;var secondPe=PK_SURF_make_sheet_body(referenceSource,box,&secondReference);var secondGe=KernelRuntime.SurfMakeSheetBody(source,*(M.PK_UVBOX_s*)&box,&secondActual);
                    Console.WriteLine($"DOMAIN_SECOND_OWNER {stem} native_error={secondPe} managed_error={secondGe}");if(secondPe!=secondGe)mismatches++;
                    if(secondPe==0&&secondGe==0)
                    {
                        if((Support(false,reference)==referenceSource)!=(Support(true,actual)==source)
                            ||(Support(false,secondReference)==referenceSource)!=(Support(true,secondActual)==source)
                            ||(Support(false,reference)==Support(false,secondReference))!=(Support(true,actual)==Support(true,secondActual)))ownershipDifferences++;
                        Console.WriteLine($"DOMAIN_IDENTITIES {stem} native_first_reuses={Support(false,reference)==referenceSource} native_second_reuses={Support(false,secondReference)==referenceSource} native_shared={Support(false,reference)==Support(false,secondReference)} managed_first_reuses={Support(true,actual)==source} managed_second_reuses={Support(true,secondActual)==source} managed_shared={Support(true,actual)==Support(true,secondActual)}");
                    }
                    if(secondPe==0&&secondGe==0){var secondFresh=Receive(Transmit(true,secondActual));CheckBody(secondReference,stem+" second native owner");CheckBody(secondFresh,stem+" second fresh GM");ParasolidBodyCompare.AssertEquivalent(secondReference,secondFresh,stem+" second owner");Check(PK_ENTITY_delete(1,&secondFresh),"delete second fresh");}
                    if(secondReference!=0)Check(PK_ENTITY_delete(1,&secondReference),"delete second native body");if(secondActual!=0)Check(KernelRuntime.EntityDelete(1,&secondActual),"delete second managed body");
                    if(args.Contains("--precision-extent",StringComparer.Ordinal))PrecisionExtent(reference,Path.Combine(output,stem+"-native-precision-input.x_t"));
                }
                if(reference!=0)Check(PK_ENTITY_delete(1,&reference),"native body delete");if(actual!=0)Check(KernelRuntime.EntityDelete(1,&actual),"managed body delete");
                PK_LOGICAL_t nativeAlive;Check(PK_ENTITY_is(referenceSource,&nativeAlive),"native source lifetime");var ownAlive=KernelRuntime.IsValidTag(source);
                Console.WriteLine($"DOMAIN_LIFETIME {stem} native_source_alive={nativeAlive==PK_LOGICAL_true} managed_source_alive={ownAlive}");
                if((nativeAlive==PK_LOGICAL_true)!=ownAlive)ownershipDifferences++;
                if(sharedFree)
                {
                    PK_LOGICAL_t spAlive;Check(PK_ENTITY_is(nativeSp,&spAlive),"native SP-owner lifetime");var ownSpAlive=KernelRuntime.IsValidTag(ownSp);
                    Console.WriteLine($"DOMAIN_SP_OWNER_LIFETIME {stem} native_alive={spAlive==PK_LOGICAL_true} managed_alive={ownSpAlive}");
                    if((spAlive==PK_LOGICAL_true)!=ownSpAlive)ownershipDifferences++;
                    ReleaseNative(nativeSp);ReleaseOwn(ownSp);ReleaseNative(nativeUv);ReleaseOwn(ownUv);
                }
                ReleaseNative(referenceSource);ReleaseOwn(source);
            }
        }
        Console.WriteLine($"DOMAIN_SUMMARY operations={runs} error_code_mismatches={mismatches} ownership_mismatches={ownershipDifferences}");if(mismatches!=0||ownershipDifferences!=0)throw new InvalidOperationException("finite domain operation or ownership contract differs");
    }
    finally{Check(KernelRuntime.SessionStop(),"managed stop");}
}
static unsafe void InspectNativeDomains(string root,string output,string[] inputs)
{
    var total=0;var bounded=0;
    using var report=new StreamWriter(Path.Combine(output,"native-face-domains.tsv"));
    report.WriteLine("source\tsha256\tface\tclass\tperiodicU\tperiodicV\tsurfaceUV\tfaceUV\tmaxNonperiodicOverrun");
    foreach(var relative in inputs)
    {
        var path=Path.GetFullPath(Path.Combine(root,relative));
        if(Path.IsPathRooted(relative)||!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new ArgumentException("project relative native input");
        var bytes=File.ReadAllBytes(path);var hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
        File.WriteAllBytes(Path.Combine(output,hash+".x_t"),bytes);
        var body=Receive(bytes);
        try
        {
            CheckBody(body,relative);int count;int* faces;Check(PK_BODY_ask_faces(body,&count,&faces),"inspect native faces");
            try
            {
                for(var i=0;i<count;i++)
                {
                    int surface,kind;Check(PK_FACE_ask_surf(faces[i],&surface),"inspect native support");Check(PK_ENTITY_ask_class(surface,&kind),"inspect support class");
                    PK_UVBOX_t definition,finite;Check(PK_SURF_ask_uvbox(surface,&definition),"inspect native domain");Check(PK_FACE_find_uvbox(faces[i],&finite),"inspect finite face domain");
                    var up=false;var vp=false;var overrun=double.NaN;
                    if(kind==PK_CLASS_bsurf)
                    {
                        PK_BSURF_sf_t sf;Check(PK_BSURF_ask(surface,&sf),"inspect native B-surface");
                        try{up=sf.is_u_periodic==PK_LOGICAL_true;vp=sf.is_v_periodic==PK_LOGICAL_true;}
                        finally{Check(PK_MEMORY_free(sf.vertex),"release inspect poles");Check(PK_MEMORY_free(sf.u_knot),"release inspect U knots");Check(PK_MEMORY_free(sf.v_knot),"release inspect V knots");Check(PK_MEMORY_free(sf.u_knot_mult),"release inspect U multiplicities");Check(PK_MEMORY_free(sf.v_knot_mult),"release inspect V multiplicities");}
                        overrun=Math.Max(up?0:Math.Max(definition.param[0]-finite.param[0],finite.param[2]-definition.param[2]),vp?0:Math.Max(definition.param[1]-finite.param[1],finite.param[3]-definition.param[3]));bounded++;
                    }
                    report.WriteLine($"{relative}\t{hash}\t{i}\t{kind}\t{up}\t{vp}\t{definition.param[0]:R}/{definition.param[1]:R}/{definition.param[2]:R}/{definition.param[3]:R}\t{finite.param[0]:R}/{finite.param[1]:R}/{finite.param[2]:R}/{finite.param[3]:R}\t{overrun:R}");total++;
                }
            }
            finally{Check(PK_MEMORY_free(faces),"release inspect faces");}
        }
        finally{Check(PK_ENTITY_delete(1,&body),"delete inspected native body");}
    }
    Console.WriteLine($"NATIVE_DOMAIN_SURVEY inputs={inputs.Length} faces={total} bsurfaces={bounded} report=native-face-domains.tsv evidence=observed-face-UV-boxes-not-a-general-domain-proof");
}
static unsafe byte[] Transmit(bool own,int body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
    Check(own?KernelRuntime.PartTransmitB(1,&body,(M.PK_PART_transmit_o_s*)&options,(M.PK_MEMORY_block_s*)&block):PK_PART_transmit_b(1,&body,&options,&block),"transmit");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}
    finally{Check(own?KernelRuntime.MemoryBlockFree((M.PK_MEMORY_block_s*)&block):PK_MEMORY_block_f(&block),"transmit release");}
}
static unsafe void BlendOwnership(bool sharedFree,bool attachedSupport,bool useMark,bool freeRootDelete)
{
    // Observe adoption and dependency lifetime only. Each implementation's
    // native spine parameter range is queried; this is no shape equivalence test.
    string? referenceSignature=null;
    double* poles=stackalloc double[18];double* knots=stackalloc double[2]{-1,1};int* um=stackalloc int[2]{3,3};int* vm=stackalloc int[2]{2,2};
    double* p=stackalloc double[4]{-.5,-.5,.5,.5};double* k=stackalloc double[2]{0,1};int* m=stackalloc int[2]{2,2};
    foreach(var own in new[]{false,true})
    {
        var signature=new System.Text.StringBuilder();for(var i=0;i<3;i++)for(var j=0;j<2;j++){var at=3*(2*i+j);poles[at]=i-1;poles[at+1]=2*j-1;poles[at+2]=(i==1?-.3:.3)+.1*(i-1)*(2*j-1);}
        var sf=new PK_BSURF_sf_t{u_degree=2,v_degree=1,n_u_vertices=3,n_v_vertices=2,vertex_dim=3,vertex=poles,n_u_knots=2,n_v_knots=2,u_knot=knots,v_knot=knots,u_knot_mult=um,v_knot_mult=vm,form=PK_BSURF_form_arbitrary_c,u_knot_type=PK_knot_non_uniform_c,v_knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c,convexity=PK_convexity_unset_c};
        int source,wall,surface;Check(own?KernelRuntime.BSurfCreate((M.PK_BSURF_sf_s*)&sf,&source):PK_BSURF_create(&sf,&source),"Blend support");
        int supportBody=0;
        if(attachedSupport)
        {
            var supportBox=new PK_UVBOX_t();supportBox.param[0]=supportBox.param[1]=-.8;supportBox.param[2]=supportBox.param[3]=.8;
            Check(own?KernelRuntime.SurfMakeSheetBody(source,*(M.PK_UVBOX_s*)&supportBox,&supportBody):PK_SURF_make_sheet_body(source,supportBox,&supportBody),"prior support owner");
        }
        var plane=new PK_PLANE_sf_t();plane.basis_set.axis=new(0,1,0);plane.basis_set.ref_direction=new(1,0,0);
        Check(own?KernelRuntime.PlaneCreate((M.PK_PLANE_sf_s*)&plane,&wall):PK_PLANE_create(&plane,&wall),"Blend wall");
        int freeSp=0,secondSp=0,freeOffset=0;
        if(sharedFree)
        {
            var uv=new PK_BCURVE_sf_t{degree=1,n_vertices=2,vertex_dim=2,vertex=p,n_knots=2,knot=k,knot_mult=m,form=PK_BCURVE_form_arbitrary_c,knot_type=PK_knot_non_uniform_c,self_intersecting=PK_self_intersect_false_c};int curve;
            Check(own?KernelRuntime.BCurveCreate((M.PK_BCURVE_sf_s*)&uv,&curve):PK_BCURVE_create(&uv,&curve),"Blend shared-support UV");var sp=new PK_SPCURVE_sf_t(source,curve);
            Check(own?KernelRuntime.SpCurveCreate((M.PK_SPCURVE_sf_s*)&sp,&freeSp):PK_SPCURVE_create(&sp,&freeSp),"Blend shared-support SP");
            var reused=own?KernelRuntime.SpCurveCreate((M.PK_SPCURVE_sf_s*)&sp,&secondSp):PK_SPCURVE_create(&sp,&secondSp);
            Console.WriteLine($"SP_BASIS_EXCLUSIVE_OWNER own={own} error={reused}");if(reused!=PK_ERROR_unsuitable_entity)throw new InvalidOperationException("used 2D SP basis accepted");
            Check(own?KernelRuntime.BCurveCreate((M.PK_BCURVE_sf_s*)&uv,&curve):PK_BCURVE_create(&uv,&curve),"second independent UV basis");sp.curve=curve;
            Check(own?KernelRuntime.SpCurveCreate((M.PK_SPCURVE_sf_s*)&sp,&secondSp):PK_SPCURVE_create(&sp,&secondSp),"Blend repeated shared-support SP");
            var displaced=new PK_OFFSET_sf_t{underlying_surface=source,offset_distance=.3};
            Check(own?KernelRuntime.OffsetCreate((M.PK_OFFSET_sf_s*)&displaced,&freeOffset):PK_OFFSET_create(&displaced,&freeOffset),"Blend shared-support offset");
        }
        var options=new PK_SURF_create_blend_o_t();var a=new PK_VECTOR_t(.5,0,.075);var b=new PK_VECTOR_t(-.5,0,.075);
        Check(own?KernelRuntime.SurfCreateBlend(source,.1,wall,.1,*(M.PK_VECTOR_s*)&a,*(M.PK_VECTOR_s*)&b,(M.PK_SURF_create_blend_o_s*)&options,&surface):PK_SURF_create_blend(source,.1,wall,.1,a,b,&options,&surface),"free Blend");
        PK_BLENDSF_sf_t definition;Check(own?KernelRuntime.BlendSfAsk(surface,(M.PK_BLENDSF_sf_s*)&definition):PK_BLENDSF_ask(surface,&definition),"Blend definition");
        if(freeRootDelete)
        {
            Check(own?KernelRuntime.EntityDelete(1,&surface):PK_ENTITY_delete(1,&surface),"delete free Blend root");
            foreach(var (label,tag) in new[]{("first-support",source),("second-support",wall),("spine",definition.spine)})
            {
                PK_LOGICAL_t alive=0;if(own)alive=KernelRuntime.IsValidTag(tag)?PK_LOGICAL_true:PK_LOGICAL_false;else Check(PK_ENTITY_is(tag,&alive),"deleted free root dependency");
                signature.Append($"{label}={alive};");Console.WriteLine($"FREE_BLEND_DELETE own={own} shared_support={sharedFree} attached_support={attachedSupport} definition={label} alive={alive==PK_LOGICAL_true}");
            }
            if(referenceSignature==null)referenceSignature=signature.ToString();else if(referenceSignature!=signature.ToString())throw new InvalidOperationException("free Blend defining-geometry deletion differs");
            if(!own){ReleaseNative(freeSp);ReleaseNative(secondSp);ReleaseNative(freeOffset);ReleaseNative(supportBody);ReleaseNative(source);ReleaseNative(wall);}
            else{ReleaseOwn(freeSp);ReleaseOwn(secondSp);ReleaseOwn(freeOffset);ReleaseOwn(supportBody);ReleaseOwn(source);ReleaseOwn(wall);}
            continue;
        }
        if(freeSp!=0)PrintSp("before",own,freeSp,source);
        PK_UVBOX_t box;Check(own?KernelRuntime.SurfAskUvbox(surface,(M.PK_UVBOX_s*)&box):PK_SURF_ask_uvbox(surface,&box),"Blend domain");
        var lower=box.param[0];var width=box.param[2]-lower;box.param[0]=lower+.4*width;box.param[2]=lower+.6*width;box.param[1]=.15;box.param[3]=.85;
        int mark=0;if(useMark)Check(own?KernelRuntime.MarkCreate(&mark):PK_MARK_create(&mark),"mark before adoption");
        int body;Check(own?KernelRuntime.SurfMakeSheetBody(surface,*(M.PK_UVBOX_s*)&box,&body):PK_SURF_make_sheet_body(surface,box,&body),"Blend sheet");
        Console.WriteLine($"BLEND_ADOPTION own={own} shared_support={sharedFree} root_reused={Support(own,body)==surface} first_input_reused={definition.geom_1==source} second_input_reused={definition.geom_2==wall} supports={source}/{wall} spine={definition.spine}");
        signature.Append($"{Support(own,body)==surface}/{definition.geom_1==source}/{definition.geom_2==wall};");
        if(!own)CheckBody(body,"native Blend sheet");
        Check(own?KernelRuntime.EntityDelete(1,&body):PK_ENTITY_delete(1,&body),"Blend body delete");
        if(freeSp!=0)
        {
            var support=PrintSp("after",own,freeSp,source);var other=PrintSp("after-second",own,secondSp,source);PK_OFFSET_sf_t displaced;
            Check(own?KernelRuntime.OffsetAsk(freeOffset,(M.PK_OFFSET_sf_s*)&displaced):PK_OFFSET_ask(freeOffset,&displaced),"surviving offset definition");
            Console.WriteLine($"BLEND_EXTERNAL_SHARING own={own} sp_pair_share={support==other} offset_shares={support==displaced.underlying_surface}");
            signature.Append($"{support==source}/{support==other}/{support==displaced.underlying_surface};");
            var uv=new PK_UV_t();var point=new PK_VECTOR_t();Check(own?KernelRuntime.SurfEval(freeOffset,*(M.PK_UV_s*)&uv,0,0,0,(M.PK_VECTOR_s*)&point):PK_SURF_eval(freeOffset,uv,0,0,PK_LOGICAL_false,&point),"surviving offset evaluation");
        }
        foreach(var (label,tag) in new[]{("root",surface),("first",source),("second",wall),("spine",definition.spine),("free-sp",freeSp)})
        { if(tag==0)continue;PK_LOGICAL_t valid=0; if(!own)Check(PK_ENTITY_is(tag,&valid),"Blend dependency lifetime");var alive=own?KernelRuntime.IsValidTag(tag):valid==PK_LOGICAL_true;Console.WriteLine($"BLEND_LIFETIME own={own} kind={label} alive={alive}");signature.Append(alive).Append(';'); }
        if(useMark)
        {
            Check(own?KernelRuntime.MarkGoto(mark):PK_MARK_goto(mark),"restore adopted geometry");
            if(freeSp!=0&&PrintSp("restored",own,freeSp,source)!=source)throw new InvalidOperationException("mark did not restore native dependency identity");
            PK_BLENDSF_sf_t restored;Check(own?KernelRuntime.BlendSfAsk(surface,(M.PK_BLENDSF_sf_s*)&restored):PK_BLENDSF_ask(surface,&restored),"restored Blend definition");
            if(restored.geom_1!=source||restored.geom_2!=wall||restored.spine!=definition.spine)throw new InvalidOperationException("restored Blend definitions differ");
            Console.WriteLine($"BLEND_MARK_RESTORED own={own} native_definition_identities=True");
        }
        if(!own)referenceSignature=signature.ToString();else if(signature.ToString()!=referenceSignature)throw new InvalidOperationException("native geometry ownership differs: "+referenceSignature+" vs "+signature);
    }
    Console.WriteLine("BLEND_OWNER_CONTRACT_MATCH geometry_equivalence=separate-oracle");
}
static unsafe PK_SURF_t PrintSp(string stage,bool own,int curve,int source)
{
    PK_SPCURVE_sf_t definition;Check(own?KernelRuntime.SpCurveAsk(curve,(M.PK_SPCURVE_sf_s*)&definition):PK_SPCURVE_ask(curve,&definition),"shared SP definition");
    Console.WriteLine($"BLEND_SHARED_SP stage={stage} own={own} original_source={definition.surf==source}");
    var p=new PK_VECTOR_t();Check(own?KernelRuntime.CurveEval(curve,.5,0,(M.PK_VECTOR_s*)&p):PK_CURVE_eval(curve,.5,0,&p),"surviving shared SP evaluation");
    return definition.surf;
}
static unsafe int Receive(byte[] bytes){fixed(byte* p=bytes){var block=new PK_MEMORY_block_t(null,(ulong)bytes.Length,p);var options=new PK_PART_receive_o_t{transmit_format=PK_transmit_format_text_c};int count;int* parts;Check(PK_PART_receive_b(block,&options,&count,&parts),"native receive");try{if(count!=1)throw new InvalidOperationException("one body");return parts[0];}finally{PK_MEMORY_free(parts);}}}
static unsafe void PrecisionExtent(PK_BODY_t reference,string path)
{
    int count;int* vertices;Check(PK_BODY_ask_vertices(reference,&count,&vertices),"native precision vertices");
    try{if(count==0)throw new InvalidOperationException("bounded sheet vertices");Check(PK_VERTEX_set_precision(vertices[0],.02),"actual native vertex precision");}
    finally{PK_MEMORY_free(vertices);}
    CheckBody(reference,"native precision input");var bytes=Transmit(false,reference);File.WriteAllBytes(path,bytes);
    var body=ReceiveOwn(bytes);
    try
    {
        if(!KernelRuntime.TryResolveBodySlot(body,out var slot)||KernelRuntime.Xt.IsOpaque(slot))throw new InvalidOperationException("editable precision sheet");
        var face=KernelRuntime.Bodies[slot].FirstFaceBody;var surface=KernelRuntime.GetSurfaceSlotByTag(KernelRuntime.Faces[face].SurfTag);
        var status=KernelRuntime.NativeDefinedSurfaceExtent(surface,out var box);if(status!=ProjectGmKernel.Native.Computation.AlgorithmStatus.Success)throw new InvalidOperationException("native definition extent "+status);
        var near=new KernelVector3{X=box.XHi+.01,Y=.5,Z=0};var far=near;far.X=box.XHi+.04;
        var nearRejected=KernelRuntime.RegularPointOutsideNativeSupport(face,near,1e-8);var farRejected=KernelRuntime.RegularPointOutsideNativeSupport(face,far,1e-8);
        Console.WriteLine($"NATIVE_PRECISION_EXTENT near_rejected={nearRejected} far_rejected={farRejected} scope=conservative-candidate-certificate");
        if(nearRejected||!farRejected)throw new InvalidOperationException("native precision omitted from finite support exclusion");
        var fresh=Receive(Transmit(true,body));try{CheckBody(fresh,"fresh native-precision GM");ParasolidBodyCompare.AssertEquivalent(reference,fresh,"native precision finite extent");}finally{PK_ENTITY_delete(1,&fresh);}
    }
    finally{Check(KernelRuntime.EntityDelete(1,&body),"delete precision receive");}
}
static unsafe PK_BODY_t ReceiveOwn(byte[] bytes)
{
    fixed(byte* p=bytes)
    {
        var block=new M.PK_MEMORY_block_s{n_bytes=(nuint)bytes.Length,bytes=p};var options=new M.PK_PART_receive_o_s{o_t_version=1,transmit_format=PK_transmit_format_text_c};int* parts;
        int count;
        Check(KernelRuntime.PartReceiveB(block,&options,&count,&parts),"direct managed native-precision receive");
        try{if(count!=1)throw new InvalidOperationException("one managed body");return parts[0];}finally{KernelRuntime.MemoryFree(parts);}
    }
}
static unsafe void ClosedSolid(string output)
{
    var queries=0;
    foreach(var scale in new[]{.125,1.7,8.0})foreach(var mirror in new[]{false,true})
    {
        var label=$"closed-torus-{scale:R}-mirror-{mirror}";var basis=new PK_AXIS2_sf_t{axis=new(.6,0,.8),ref_direction=new(0,1,0)};int reference,body,copy;
        Check(PK_BODY_create_solid_torus(2,.5,&basis,&reference),"independent native torus");Check(KernelRuntime.BodyCreateSolidTorus(2,.5,(M.PK_AXIS2_sf_s*)&basis,&body),"independent managed torus");
        Check(PK_ENTITY_copy(reference,&copy),"native solid copy");Check(PK_ENTITY_delete(1,&reference),"native solid source delete");reference=copy;
        Check(KernelRuntime.EntityCopy(body,&copy),"managed solid copy");Check(KernelRuntime.EntityDelete(1,&body),"managed solid source delete");body=copy;
        var form=new M.PK_TRANSF_sf_s();form.matrix[0]=(mirror?-1:1)*scale;form.matrix[5]=form.matrix[10]=scale;form.matrix[15]=1;form.matrix[3]=3;form.matrix[7]=5;form.matrix[11]=7;
        Transform(false,reference,form);Transform(true,body,form);
        var actual=Transmit(true,body);var expected=Transmit(false,reference);File.WriteAllBytes(Path.Combine(output,label+"-gm.x_t"),actual);File.WriteAllBytes(Path.Combine(output,label+"-pk.x_t"),expected);
        var fresh=Receive(actual);try{CheckBody(fresh,label+" GM fresh");CheckBody(reference,label+" reference");ParasolidBodyCompare.AssertEquivalent(reference,fresh,label);}finally{PK_ENTITY_delete(1,&fresh);}
        Check(KernelRuntime.EntityDelete(1,&body),"managed torus reload source delete");body=ReceiveOwn(actual);
        Check(PK_ENTITY_delete(1,&reference),"native torus reload source delete");reference=Receive(expected);
        foreach(var (x,y,z) in new[]{(0.0,0.0,0.0),(0.0,2.0,0.0),(0.0,3.0,0.0),(0.0,1.0,0.0),(0.0,2.5,0.0),(0.0,1.5,0.0),(.3,2.0,.4),(0.0,2.4999,0.0),(0.0,2.5001,0.0)})
        {
            var point=new PK_VECTOR_t(3+(mirror?-1:1)*scale*x,5+scale*y,7+scale*z);int nativeEnclosure,ownEnclosure,nativeTopology,ownTopology,nativeClass,ownClass;
            Check(PK_BODY_contains_vector(reference,point,&nativeEnclosure,&nativeTopology),"native complete torus query");Check(KernelRuntime.BodyContainsVector(body,*(M.PK_VECTOR_s*)&point,&ownEnclosure,&ownTopology),"managed complete torus query");
            Check(PK_ENTITY_ask_class(nativeTopology,&nativeClass),"native query topology");Check(KernelRuntime.EntityAskClass(ownTopology,&ownClass),"managed query topology");queries++;
            Console.WriteLine($"CLOSED_SOLID_QUERY {label} input={x:R}/{y:R}/{z:R} native={nativeEnclosure}/{nativeClass} managed={ownEnclosure}/{ownClass}");
            if(nativeEnclosure!=ownEnclosure||nativeClass!=ownClass)throw new InvalidOperationException("closed torus material or boundary classification differs");
        }
        Check(KernelRuntime.EntityDelete(1,&body),"delete reloaded managed solid");Check(PK_ENTITY_delete(1,&reference),"delete reloaded native solid");
    }
    Console.WriteLine($"CLOSED_SOLID_PASS native_queries={queries} lifecycle=oblique-frame-copy-delete-mirror-scale-native-XT-reload");
}
static unsafe void Transform(bool own,PK_BODY_t body,M.PK_TRANSF_sf_s form)
{
    int tag;Check(own?KernelRuntime.TransfCreate(&form,&tag):PK_TRANSF_create((PK_TRANSF_sf_t*)&form,&tag),"solid mirror-scale transform");int count;int* replacements;PK_LOGICAL_t* exact;
    Check(own?KernelRuntime.BodyTransform(body,tag,0,&count,&replacements,(byte**)&exact):PK_BODY_transform(body,tag,0,&count,&replacements,&exact),"transform complete native solid");
    if(replacements!=null)Check(own?KernelRuntime.MemoryFree(replacements):PK_MEMORY_free(replacements),"free solid replacements");if(exact!=null)Check(own?KernelRuntime.MemoryFree(exact):PK_MEMORY_free(exact),"free solid exactness");Check(own?KernelRuntime.EntityDelete(1,&tag):PK_ENTITY_delete(1,&tag),"delete solid transform");
}
static unsafe PK_SURF_t Support(bool own,PK_BODY_t body){int n;int* faces;Check(own?KernelRuntime.BodyAskFaces(body,&n,&faces):PK_BODY_ask_faces(body,&n,&faces),"faces for source identity");try{if(n!=1)throw new InvalidOperationException("one regular sheet face");PK_SURF_t surface;Check(own?KernelRuntime.FaceAskSurf(faces[0],&surface):PK_FACE_ask_surf(faces[0],&surface),"face geometry identity");return surface;}finally{if(own)KernelRuntime.MemoryFree(faces);else PK_MEMORY_free(faces);}}
static unsafe void ReleaseNative(PK_ENTITY_t tag){PK_LOGICAL_t alive;Check(PK_ENTITY_is(tag,&alive),"native cleanup ownership query");if(alive==PK_LOGICAL_true)Check(PK_ENTITY_delete(1,&tag),"native live orphan cleanup");}
static unsafe void ReleaseOwn(PK_ENTITY_t tag){if(KernelRuntime.IsValidTag(tag))Check(KernelRuntime.EntityDelete(1,&tag),"managed live orphan cleanup");}
static unsafe void CheckBody(int body,string label){var options=new PK_BODY_check_o_t{max_faults=100};int n;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&n,&faults),"full check");try{Console.WriteLine($"FULL_CHECK {label} faults={n}");if(n!=0)throw new InvalidOperationException(label+" native full check");}finally{PK_MEMORY_free(faults);}}
static void Check(int error,string label){if(error!=0)throw new InvalidOperationException(label+" error="+error);}
static string Source([CallerFilePath]string file="")=>file;

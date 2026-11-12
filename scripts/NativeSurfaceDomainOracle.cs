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
        if(args.Contains("--blend-owner",StringComparer.Ordinal)) { BlendOwnership(sharedFree,args.Contains("--attached-support",StringComparer.Ordinal),args.Contains("--mark",StringComparer.Ordinal)); return; }
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
static unsafe byte[] Transmit(bool own,int body)
{
    var options=new PK_PART_transmit_o_t{transmit_format=PK_transmit_format_text_c,transmit_version=371};PK_MEMORY_block_t block;
    Check(own?KernelRuntime.PartTransmitB(1,&body,(M.PK_PART_transmit_o_s*)&options,(M.PK_MEMORY_block_s*)&block):PK_PART_transmit_b(1,&body,&options,&block),"transmit");
    try{using var stream=new MemoryStream();for(var p=&block;p!=null;p=p->next)stream.Write(new ReadOnlySpan<byte>(p->bytes,checked((int)p->n_bytes)));return stream.ToArray();}
    finally{Check(own?KernelRuntime.MemoryBlockFree((M.PK_MEMORY_block_s*)&block):PK_MEMORY_block_f(&block),"transmit release");}
}
static unsafe void BlendOwnership(bool sharedFree,bool attachedSupport,bool useMark)
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
static unsafe PK_SURF_t Support(bool own,PK_BODY_t body){int n;int* faces;Check(own?KernelRuntime.BodyAskFaces(body,&n,&faces):PK_BODY_ask_faces(body,&n,&faces),"faces for source identity");try{if(n!=1)throw new InvalidOperationException("one regular sheet face");PK_SURF_t surface;Check(own?KernelRuntime.FaceAskSurf(faces[0],&surface):PK_FACE_ask_surf(faces[0],&surface),"face geometry identity");return surface;}finally{if(own)KernelRuntime.MemoryFree(faces);else PK_MEMORY_free(faces);}}
static unsafe void ReleaseNative(PK_ENTITY_t tag){PK_LOGICAL_t alive;Check(PK_ENTITY_is(tag,&alive),"native cleanup ownership query");if(alive==PK_LOGICAL_true)Check(PK_ENTITY_delete(1,&tag),"native live orphan cleanup");}
static unsafe void ReleaseOwn(PK_ENTITY_t tag){if(KernelRuntime.IsValidTag(tag))Check(KernelRuntime.EntityDelete(1,&tag),"managed live orphan cleanup");}
static unsafe void CheckBody(int body,string label){var options=new PK_BODY_check_o_t{max_faults=100};int n;PK_check_fault_t* faults;Check(PK_BODY_check(body,&options,&n,&faults),"full check");try{Console.WriteLine($"FULL_CHECK {label} faults={n}");if(n!=0)throw new InvalidOperationException(label+" native full check");}finally{PK_MEMORY_free(faults);}}
static void Check(int error,string label){if(error!=0)throw new InvalidOperationException(label+" error="+error);}
static string Source([CallerFilePath]string file="")=>file;

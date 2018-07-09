#version 330

//external
SCREEN_WIDTH
SCREEN_HEIGHT
MAX_VIEWSPACE_MESHES
NUM_SAMPLES
MAX_LIGHTS
//external end

#define FLT_MAX 3.402823466e+38 
#define FLT_MIN -3.402823466e+37 

#define when_eq(x, y)  (1.0 - abs(sign(x - y)))
#define when_neq(x, y) (abs(sign(x - y)))      
#define when_gt(x, y)  (max(sign(x - y), 0.0)) 
#define when_lt(x, y)  (max(sign(y - x), 0.0)) 
#define when_ge(x, y)  (1.0 - when_lt(x, y))   
#define when_le(x, y)  (1.0 - when_gt(x, y))   

#define and(a, b)  (a * b)            
#define or(a, b)   (min(a + b, 1.0))  
#define xor(a, b)  (mod((a + b), 2.0))
#define not(a)     (1.0 - a)        

out vec4 outColor;

uniform int numBoundingBoxes;
layout(std140) uniform BoundingBoxes
{
    vec4 Bounds[MAX_VIEWSPACE_MESHES];
};
layout(std140) uniform Orientations
{
    mat4 Orients[MAX_VIEWSPACE_MESHES];
};
layout(std140) uniform SampleHemisphere
{
    vec2 Samples[NUM_SAMPLES];
};

uniform sampler2D noisemap;

uniform sampler3D sdf;
uniform sampler3D sdfScene;
uniform sampler3D sdfSceneHits;

uniform int sceneGridSize = 512;
uniform int iFrame;

uniform mat4 viewMatrix;
uniform vec3 viewPos;

const vec3 sunDir 	  = normalize(vec3(0.0,1.0,0.0));
const vec3 sunCol 	  =  6.0*vec3(1.0,0.8,0.6);
const vec3 skyCol 	  =  4.0*vec3(0.2,0.35,0.5);
const vec3 surfaceColor   = vec3(0.4)*vec3(1.2,1.1,1.0);

float hmax(const in vec3 p) { return max(p.x,max(p.y,p.z)); }
float hmin(const in vec3 p) { return min(p.x,min(p.y,p.z)); }
float hmax(const in vec2 p) { return max(p.x,p.y); }
float hmin(const in vec2 p) { return min(p.x,p.y); }

float insideBound(const in vec3 v, const in vec3 topRight) { return hmin( step(0, v) - step(topRight, v) ); }
float insideBound(const in vec2 v, const in vec2 topRight) { return hmin( step(0, v) - step(topRight, v) ); }

vec3 cosineDirection( const in vec3 nor, int index)
{
    vec2 uv = Samples[index];
    
    float a = 6.2831853 * uv.y;
    uv.x = 2.0*uv.x - 1.0;
    return normalize( nor + vec3(sqrt(1.0-uv.x*uv.x) * vec2(cos(a), sin(a)), uv.x) );
}

float map( in vec3 cr, float exitStep, const in vec3 bbox_inv )
{
    cr *= bbox_inv;
    return (insideBound(cr, vec3(1.0)) == 0.0) ? exitStep : texture(sdf, cr).r;
}

float map( in vec3 cr, const in vec3 bbox_inv )
{
    return texture(sdf, cr * bbox_inv).r;
}

vec3 calcModelNormal( in vec3 cr, const in vec3 bbox_inv )
{
    vec4 eps = vec4(bbox_inv, 0.0);
    cr *= bbox_inv;

    float phi = texture( sdf, cr ).r;
    return normalize( vec3(
      texture( sdf, cr+eps.xww ).r - phi,
      texture( sdf, cr+eps.wyw ).r - phi,
      texture( sdf, cr+eps.wwz ).r - phi ) );
}

vec3 calcSceneNormal( in vec3 cr )
{
    vec3 eps = vec3(1.0/float(sceneGridSize), 0.0, 0.0);

    float phi = texture( sdfScene, cr ).r;
    return normalize( vec3(
      texture( sdfScene, cr+eps.xyy ).r - phi,
      texture( sdfScene, cr+eps.yxy ).r - phi,
      texture( sdfScene, cr+eps.yyx ).r - phi ) );
}

vec3 calcNormal_deriv( in vec3 pos)
{
    return normalize( cross( dFdy(pos), dFdx(pos) ) );
}

const float scale = 2.56;
float scene_shadow(const in vec3 ro, const in vec3 rd, in vec2 ignore, float st, float k)
{
    float h = 0.0; float res = 1.0;

    vec3 lro = ro/scale; vec3 lrd=rd/scale;
    for(int i=0; i<35; i++ )
    {
        vec3 cr = (lro+st*lrd)/sceneGridSize + 0.5;	
	if(insideBound(cr, vec3(1.0)) == 0.0) return res;
	h = texture(sdfScene, cr).r;
        if( h<0.0001 ) {
	    if( texture(sdfSceneHits, cr).rg == ignore) { return 0.0; }
	    else { return 0.0; }
	}
	res = min( res,k*h/st );
	st += h;
    }

    return res;
}

float intersectModel(const in vec3 ro, const in vec3 rd, 
	   	     const in vec3 bbox_inv, float t)
{
    float h = 10.0; int nclose = 0;
    for(int i=0; i<10; i++ )
    {
        h = map(ro+rd*t, 20.5f, bbox_inv);
	if( h < 2.5 ) nclose++;
	if( nclose >= 3 ) return t;
        if( abs(h)<0.01 ) return t;
        t += h;
    }
    
    return -1.0;
}

float intersectScene(const in vec3 ro, const in vec3 rd, out vec3 bbox_inv, out vec3 mro, 
		     out vec3 mrd, out int modelIndex, inout vec2 ignore, float st)
{
    float h = 0.0;

    vec3 lro = ro/scale; vec3 lrd=rd/scale; int nclose = 0;
    for(int i=0; i<40; i++ )
    {
        vec3 cr = (lro+st*lrd)/(sceneGridSize) + 0.5;	
	if(insideBound(cr, vec3(1.0)) == 0.0) { st += 200; continue; }
	h = texture(sdfScene, cr).r;
	if( h < 4.0 ) nclose++;
        if( h<0.01 || nclose >= 10 )
	{
	    vec2 hits = texture(sdfSceneHits, cr).rg;
            ignore = hits;
	    
	    modelIndex = int(hits.x);

            mro = (Orients[modelIndex] * vec4(ro,1.0f)).xyz;
            mrd = mat3(Orients[modelIndex]) * rd;
    	    bbox_inv = 1 / Bounds[modelIndex].xyz;

	    return intersectModel(mro, mrd, bbox_inv, st); 
	}

	else st += h;
    }

    return -1.0;
}

float shadow_soft( const in vec3 roScene, const in vec3 rdScene, 
	      in vec3 roModel, in vec3 rdModel, 
	      in vec3 bbox_inv, in vec2 ignore, float ko, float ks )
{
    float tmax = 400.0; float res = 1.0;
    
    // Self-Shadowing
    float tself = 0.01;
    for( int i=0; i<30; i++ )
    {
        float h = map(roModel+rdModel*tself, 50.0f, bbox_inv);
	if( h>=50.0) break;
	if( h<0.001) break;
	res = min( res, ks*h/tself );
        tself += h;
    }
 
    return smoothstep(res, 0.5, 0.4);
}


float shadow( const in vec3 roScene, const in vec3 rdScene, 
	      in vec3 roModel, in vec3 rdModel, 
	      in vec3 bbox_inv, in vec2 ignore, float ko, float ks )
{
    float tmax = 400.0; float res = 1.0;
    
    // Self-Shadowing
    float tself = 0.01;
    for( int i=0; i<30; i++ )
    {
        float h = map(roModel+rdModel*tself, 50.0f, bbox_inv);
	if( h>=50.0) break;
	if( h<0.001) { res = 0.0001; break; }
	res = min( res, ks*h/tself );
        tself += h;
    }
 
    // Other-Shadowing
    float st = 1.9; 
    float res2 = scene_shadow(roScene, rdScene, ignore, st, ko);
   
    return min(res, res2);
}

// **************** TERRAIN ***************************

uniform sampler2D iChannel0;
uniform sampler2D iChannel1;
uniform sampler2D iChannel3;

uniform sampler3D terrainSDF;

vec4 orb;
float MapFractal( vec3 p )
{
    float scale = 1.0;
    
    orb = vec4(1000.0); 
    
    for( int i=0; i<6;i++ )
    {
        p = -1.0 + 2.0*fract(0.5*p+0.5);
    
        float r2 = dot(p,p);
        
        orb = min( orb, vec4(abs(p),r2) );
        
        float k = 1.5/r2;
        p     *= k;
        scale *= k;
    }
    
    return 0.25*abs(p.y)/scale;
}

const float terrainscale = 16.0;
const float fractscale = 200.0;
vec2 intersectFractal( in vec3 ro, in vec3 rd, in float tmin, in float tmax )
{
    float tt = tmin;
    vec3 rot = ro / (vec3(1024.0, 1024.0/8.0, 1024.0) * terrainscale); 
    vec3 rdt = rd / (vec3(1024.0, 1024.0/8.0, 1024.0) * terrainscale);
    for( int i=0; i<120; i++ )
    {
        vec3 post = (rot + tt*rdt) + 0.5;
	post.y = (post.y - 1.1) * 0.5 + 1.0;
	if( insideBound(post, vec3(1.0)) == 0.0) break;
        float rest = texture( terrainSDF, post ).r;
        if( rest<(0.001*tt) )
	{
    	    float maxd = tt + 2000.0;
    	    float t = tt + 0.01;
	    ro /= fractscale; rd /= fractscale;
	    for (int i = 0; i<80; i++)
	    {
    	    	vec3 pos = ro + t*rd;
	    	float h = MapFractal( pos ) * fractscale;
            	if( h<0.001*t ) break;
		if( t>maxd )	return vec2(10000.0, 0.1);
            	t += h;
	    }

	    return vec2(t, 0.1);
	}
	if( tt>tmax ) break;
        tt += rest*(4.0 + i * 0.02);
    }

    return vec2(10000.0, 0.1);
}

const vec3 eps = vec3( 1.0/100.0, 0.0, 0.0 );
vec3 calcNormalHigh( in vec3 pos, float t )
{
    pos /= fractscale;
    float phi = MapFractal( pos );
    return normalize( vec3( 
		MapFractal( pos + eps.xyy ) - phi, 
		MapFractal( pos + eps.yxy ) - phi, 
		MapFractal( pos + eps.yyx ) - phi ) ); 
}

const vec3 sig = vec3( 1.0, 0.0, 0.0 );
vec3 calcNormalMed( in vec3 pos, float t )
{
    pos /= fractscale;
    float phi = MapFractal( pos );
    return normalize( vec3( 
		MapFractal( pos + sig.xyy ) - phi, 
		MapFractal( pos + sig.yxy ) - phi, 
		MapFractal( pos + sig.yyx ) - phi ) ); 
}

vec3 dome( in vec3 rd, in vec3 light1 )
{
    float sda = clamp(0.5 + 0.5*dot(rd,light1),0.0,1.0);
    float cho = max(rd.y,0.0);
    
    vec3 bgcol = mix( mix(vec3(0.00,0.40,0.60)*0.7, 
                          vec3(0.80,0.70,0.20),                        pow(1.0-cho,3.0 + 4.0-4.0*sda)), 
                          vec3(0.43+0.2*sda,0.4-0.1*sda,0.4-0.25*sda), pow(1.0-cho,10.0+ 8.0-8.0*sda) );

    bgcol *= 0.8 + 0.2*sda;
    return bgcol*0.75;
}

const vec3 light1 = normalize( vec3(-0.8,0.2,0.5) );
vec3 terrain(const in vec3 ro, const in vec3 rd)
{
    // background    
    vec3 bgcol = dome( rd, light1 );
    
    // raymarch
    float tmin = 10.0;
    float tmax = 4500.0;
    
    float maxh = 130.0;
    float tp = (maxh-ro.y)/rd.y;
    if( tp>0.0 )
    {
        if( ro.y>maxh ) tmin = max( tmin, tp );
        else            tmax = min( tmax, tp );
    }
    
    float sundotc = clamp( dot(rd,light1), 0.0, 1.0 );
    vec3  col = bgcol;
    
    vec2 res = intersectFractal( ro, rd, tmin, tmax );

    if( res.x>tmax )
    {
        // sky		
		col += 0.2*0.12*vec3(1.0,0.5,0.1)*pow( sundotc,5.0 );
		col += 0.2*0.12*vec3(1.0,0.6,0.1)*pow( sundotc,64.0 );
		col += 0.2*0.12*vec3(2.0,0.4,0.1)*pow( sundotc,512.0 );

        // clouds
  		vec2 sc = ro.xz + rd.xz*(1000.0-ro.y)/rd.y;
		col = mix( col, 0.25*vec3(0.5,0.9,1.0), 0.4*smoothstep(0.0,1.0,texture(iChannel0,0.000005*sc).x) );

        // sun scatter
        col += 0.2*0.2*vec3(1.5,0.7,0.4)*pow( sundotc, 4.0 );
    }
    else
    {
        // mountains		
        float t = res.x;
	vec3 pos = ro + t*rd;
        vec3 nor = calcNormalHigh( pos, t );
        vec3 sor = calcNormalMed( pos, t );
        vec3 ref = reflect( rd, nor );

        // rock
        col = vec3(0.07,0.06,0.05);
	col *= 0.2 + sqrt( texture( iChannel0, 0.01*pos.xy*vec2(0.5,1.0) ).x *
                           texture( iChannel0, 0.01*pos.zy*vec2(0.5,1.0) ).x );
        vec3 col2 = vec3(1.0,0.2,0.1)*0.01;
        col = mix( col, col2, 0.5*res.y );
        
        // grass
        float s = smoothstep(0.6,0.7,nor.y - 0.01*(pos.y-20.0));        
        s *= smoothstep( 0.15,0.2,0.01*nor.x+texture(iChannel0, 0.001*pos.zx).x);
        vec3 gcol = 0.13*vec3(0.22,0.23,0.04);
        gcol *= 0.3+texture( iChannel1, 0.03*pos.xz ).x*1.4;
        col = mix( col, gcol, s );
        nor = mix( nor, sor, 0.3*s );
        vec3 ptnor = nor;

        // lighting		
        float amb = clamp( nor.y,0.0,1.0);
	float dif = clamp( dot( light1, nor ), 0.0, 1.0 );
	float bac = clamp( dot( normalize( vec3(-light1.x, 0.0, light1.z ) ), nor ), 0.0, 1.0 );
	float sha = 0.9;
        float spe = pow( clamp( dot(ref,light1), 0.0, 1.0 ), 4.0 ) * dif;
		
	vec3 lin  = vec3(0.0);
        lin += dif*vec3(11.0,6.00,3.00)*vec3( sha, sha*sha*0.5+0.5*sha, sha*sha*0.8+0.2*sha );
	lin += amb*vec3(0.25,0.30,0.40);
        lin += bac*vec3(0.35,0.40,0.50);
        lin += spe*vec3(4.00,4.00,4.00)*res.y;
        
        col *= lin;

        // fog
        col = mix( col, 0.25*mix(vec3(0.4,0.75,1.0),vec3(0.3,0.3,0.3), sundotc*sundotc), 1.0-exp(-0.0000001*t*t) );

        // sun scatter
        col += 0.15*vec3(1.0,0.8,0.3)*pow( sundotc, 8.0 )*(1.0-exp(-0.003*t));

        // background
        col = mix( col, bgcol, 1.0-exp(-0.00000004*t*t) );
    }

    return col;
}


// ************ MAIN ********************

vec3 calcRayCol(const in vec3 scenePos, const in vec3 sceneNor, const in vec3 modelPos, 
		const in vec3 modelNor, const in vec3 sceneLight, const in vec3 modelLight, 
		const in vec3 bbox_inv, const in vec2 ignore)
{
   const float shadowBias = 1.0;

   float sunDif = max(0.0, dot(modelLight, modelNor));
   float sunSha = 1.0; 
   if( sunDif > 0.00001 ) sunSha = 
	shadow(scenePos+20*shadowBias*sceneLight, sceneLight, modelPos+2*shadowBias*modelLight, 
	       modelLight, bbox_inv, ignore, 0.55, 0.1);

   float skySha = 1.0f;
   // Multple samples may provide better results at a major performance cost...
   for (int sDir = 0; sDir < 1; sDir++) {
        const vec3 env = vec3(0,1,0);

	skySha = shadow_soft(scenePos+20*shadowBias*env, env, modelPos+shadowBias*-env, 
	                  -env, bbox_inv, ignore, 0.9, 0.01);
   }

   return (skyCol * skySha * 0.07) + (4 * sunDif * sunSha * sunCol);
}

vec3 castRay( const in vec3 ro, const in vec3 rd )
{
    vec3 colorMask = vec3(1.0);
    vec3 cAcc = vec3(0.0);

    float t = -1.0; vec2 ignoreBox = vec2(30.0); float st = 0.001; int modelIndex;
    vec3 bbox_inv = vec3(-1.0); vec3 roModel; vec3 rdModel;
    t = intersectScene( ro, rd, bbox_inv, roModel, rdModel, modelIndex, ignoreBox, st);

    if ( t < 0.0 ) return terrain(ro, rd);

    float fdis = t;

    vec3 modelPos = roModel + rdModel * t;
    vec3 scenePos = ro + rd * t;
    vec3 modelNor = calcModelNormal( modelPos, bbox_inv );
    vec3 sceneNor = calcSceneNormal( scenePos / (scale * sceneGridSize) + 0.5 );

    // #TODO Model light directions can be pre-calculated
    vec3 modelSun = normalize(mat3(Orients[modelIndex]) * sunDir);

    colorMask *= surfaceColor;

    cAcc += calcRayCol(scenePos, sceneNor, modelPos, modelNor, sunDir, 
		       modelSun, bbox_inv, ignoreBox) * colorMask;
    return cAcc;

    roModel = modelPos;

    // GI Rays:
//    colorMask *= surfaceColor;
//    vec3 bcAcc = vec3(0);
//    for (int iDir = 0; iDir < NUM_SAMPLES; iDir++)
//    {
//	vec3 brd = cosineDirection(1, nor, iDir);
//        float bt = softIntersectInSDF( roModel, brd, bbox_inv );
//	if (bt < 0.0001) break;
//    	vec3 bpos = roModel + brd * bt;
//	vec3 bnor = calcNormal(bpos, bbox_inv );
//        bcAcc += calcRayCol(bpos, bnor, bbox_inv );
//    }
//    bcAcc = (bcAcc * colorMask) / NUM_SAMPLES;
//    cAcc += bcAcc;

    float ff = exp(-0.00001*fdis*fdis);
    cAcc *= ff; 
    cAcc += (1.0-ff)*0.05*vec3(0.9,1.0,1.0);
    return cAcc;
}


const vec2 iResolution = vec2(SCREEN_WIDTH, SCREEN_HEIGHT);
void main()
{
    vec3 uvw = ( vec3( (gl_FragCoord.xy - 0.5) / iResolution, 1.0f) * 2 - 1); 
    uvw.r *= (SCREEN_WIDTH / SCREEN_HEIGHT);

    outColor = vec4( pow( pow( castRay(viewPos, normalize(uvw) * mat3(viewMatrix)), vec3(0.4545) )
		          , vec3(0.8, 0.85, 0.9) ), 1.0);

}

#if 0
// Dynamic intersections (slow)

void intersectScene_dynamic()
{
    vec3 BoundingBox;
    HitRecord hlist[5];
    float ctmin = FLT_MAX; float ctmax = 0; int dl = 0; int dr = 0;
    for (int i = 0; i < numBoundingBoxes; i++) {
	vec3 rro = ro; vec3 rrd = rd; vec3 bbox;
	float nt = intersectAABB(rro, rrd, i, bbox) + 0.1;
	if (nt == -1.0) continue;

	int o = 0;
	if (nt < ctmin) { o = max(dl--,-2); ctmin = nt; }
	if (nt > ctmax) { o = min(dr++, 2); ctmax = nt; }
        hlist[o+2] = HitRecord(nt, rro, rrd, bbox);
    }
}

float intersectAABB(inout vec3 o, inout vec3 d, int index, out vec3 BoundingBox_inv) 
{
    mat4 orient = Orients[index];
    vec3 bound = Bounds[index].xyz;
    
    vec3 roModel = (orient * vec4(o,1.0f)) .xyz;
    vec3 rdModel = (orient * vec4(d,0.0f)) .xyz;

    if( cless(roModel, bound) && (hmin(roModel) >= 0) ) {
	o = roModel;
	d = rdModel;
	BoundingBox_inv = 1 / bound;
	return 0.0;
    }

    vec3 id = 1 / (rdModel + vec3(0.000001f));

    vec3 t1 = (      - roModel) * id;
    vec3 t2 = (bound - roModel) * id;

    float tmin = hmax( min(t1, t2) );
    float tmax = hmin( max(t1, t2) );

    if( tmax >= tmin && tmin >= 0.0f ) {
	o = roModel;
	d = rdModel;
	BoundingBox_inv = 1 / bound;
	return tmin;
    }
    else return -1.0;
}
#endif


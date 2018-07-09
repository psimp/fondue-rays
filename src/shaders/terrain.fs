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

out vec4 fragColor;

uniform float iTime;

uniform mat4 viewMatrix;
uniform vec3 viewPos;

float hmax(const in vec3 p) { return max(p.x,max(p.y,p.z)); }
float hmin(const in vec3 p) { return min(p.x,min(p.y,p.z)); }
float hmax(const in vec2 p) { return max(p.x,p.y); }
float hmin(const in vec2 p) { return min(p.x,p.y); }
float insideBound(const in vec3 v, const in vec3 topRight) { return hmin( step(0, v) - step(topRight, v) ); }
float insideBound(const in vec2 v, const in vec2 topRight) { return hmin( step(0, v) - step(topRight, v) ); }

const vec2 iResolution = vec2(SCREEN_WIDTH, SCREEN_HEIGHT);
const vec2 iMouse = vec2(0.0);

#define USE_BOUND_PLANE

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

const float scale = 16.0;
const float fractscale = 200.0;
vec2 intersectFractal( in vec3 ro, in vec3 rd, in float tmin, in float tmax )
{
    float tt = tmin;
    vec3 rot = ro / (vec3(1024.0, 1024.0/8.0, 1024.0) * scale); 
    vec3 rdt = rd / (vec3(1024.0, 1024.0/8.0, 1024.0) * scale);
    for( int i=0; i<120; i++ )
    {
        vec3 post = (rot + tt*rdt) + 0.5;
	post.y = (post.y - 0.5) * 0.5 + 1.0;
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

void main()
{
    vec2 xy = -1.0 + 2.0*gl_FragCoord.xy/iResolution.xy;
    vec2 sp = xy*vec2(iResolution.x/iResolution.y,1.0);

    vec3 ro = viewPos;
    vec3 rd = normalize(vec3(sp.xy,1.5)) * mat3(viewMatrix);
   
    vec3 col = terrain(ro, rd);

    // gamma
    col = pow( col, vec3(0.45) );
 
    // color grading    
    col = col*1.4*vec3(1.0,1.0,1.02) + vec3(0.0,0.0,0.11);
    col = clamp(col,0.0,1.0);
    col = col*col*(3.0-2.0*col);
    col = mix( col, vec3(dot(col,vec3(0.333))), 0.25 );
    
    fragColor = vec4( col, 1.0 );
}

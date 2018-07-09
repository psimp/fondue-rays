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

uniform sampler2D grassDecal;
uniform sampler2D tubeDecal;

uniform float gridSize;
uniform uint layer;

const mat2 m2 = mat2(1.6,-1.2,1.2,1.6);

float noi( in vec2 p )
{
    return 0.5*(cos(6.2831*p.x) + cos(6.2831*p.y));
}

float terrainHigh( vec2 p )
{
    vec2 q = p;
    p *= 0.0013;

    float s = 1.0;
    float t = 0.0;
    for( int i=0; i<7; i++ )
    {
        t += s*noi( p );
    	s *= 0.5 + 0.1*t;
        p = 0.97*m2*p + (t-0.5)*0.2;
    }
    
    t +=   0.05*textureLod( grassDecal, 0.001*q, 0.0 ).x;
    t +=   0.03*textureLod( grassDecal, 0.005*q, 0.0 ).x;
    t += t*0.03*textureLod( grassDecal, 0.020*q, 0.0 ).x;

    return t*55.0;
}

float tubes( vec3 pos )
{
    float sep = 400.0;

    pos.z -= sep*0.025*noi( 0.005*pos.xz*vec2(0.5,1.5) );
    pos.x -= sep*0.050*noi( 0.005*pos.zy*vec2(0.5,1.5) );
    
    vec3 qos = mod( pos + sep*0.5, sep ) - sep*0.5; 
    qos.y = pos.y - 70.0;
    qos.x += sep*0.3*sin( 0.01*pos.z );
    qos.y += sep*0.1*sin( 0.01*pos.x );

    float sph = length( qos.xy ) - sep*0.012;

    sph -= (1.0-0.8*smoothstep(-10.0,0.0,qos.y))*sep*0.003*noi( 0.15*pos.xy*vec2(0.2,1.0) );

    return sph;
}

float tubesH( vec3 pos )
{
    float t = tubes( pos );

    t += 1.0*texture( tubeDecal, 0.01*pos.yz ).x;
    t += 2.0*texture( grassDecal, 0.005*pos.xy ).x;

    return t;
}

float mapH( in vec3 pos )
{
    float y = terrainHigh(pos.xz);
        
    float h = pos.y - y;
    
    float sph = tubesH( pos );
    float k = 60.0;
    float w = clamp( 0.5 + 0.5*(h-sph)/k, 0.0, 1.0 );
    h = mix( h, sph, w ) - k*w*(1.0-w);

    return h;
}

void main()
{
    vec2 fragCoord = vec2(gl_FragCoord.x, (gl_FragCoord.y - 100.0));

    vec4 closest = vec4(FLT_MAX);
    vec2 index = vec2(30.0);
    vec3 from = 2.0 * vec3(fragCoord - fragCoord/2, float(layer)); 

    fragColor = vec4( mapH(from), 1.0, 1.0, 1.0 );
}

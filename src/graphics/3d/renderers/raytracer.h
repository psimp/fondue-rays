#pragma once

#include "raymarch.h"
#include "terrain.h"
#include "scenevoxelizer.h"
#include "terrainvoxelizer.h"

namespace Fondue { namespace graphics {

    class Raytracer
	{

	private:

        RayMarch rm;
        SceneVoxelizer sceneBBoxer;

	public:

        Raytracer()
        {

        }

        ~Raytracer() { }

        void init_GeometryDependant()
        {
            TerrainVoxelizer voxelizer;
            voxelizer.gensdf();

            sceneBBoxer.gensdf();
            rm.init();
        }

        void draw(float time)
        {
            glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
            rm.draw(time);
        }

	};

} }

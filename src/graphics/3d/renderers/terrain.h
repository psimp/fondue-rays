#ifndef TERRAIN_H
#define TERRAIN_H

#include "renderpass.h"

namespace Fondue { namespace graphics {

#define HEMISPHERE_SAMPLES_BINDING 4

    class Terrain : public RenderPass<EMPTY, EMPTY>
    {

        GLuint gVoxelMap, gAccumulator, captureFBO, captureRBO;
        GLuint mFrameQuadVAO = rprims.getQuad();

    public:

        __attribute__((always_inline)) void init()
        {
            mShader->enable();
            mShader->setUniform1i("iChannel0", CH0_MAP);
            mShader->setUniform1i("iChannel1", CH1_MAP);
            mShader->setUniform1i("iChannel3", CH3_MAP);
            mShader->setUniform1i("terrainSDF", TERRAIN_SDF);
            mShader->disable();
        }

        __attribute__((always_inline)) void draw(float time)
        {
            mShader->enable();
            mShader->setUniformMat4("viewMatrix",       Camera::current_camera->getViewMatrix());
            mShader->setUniform3f("viewPos",            Camera::current_camera->getPosition());
            mShader->setUniform1f("iTime", time);

            glBindVertexArray(mFrameQuadVAO);
            glDrawArrays(GL_TRIANGLE_STRIP, 0, 4);
            glBindVertexArray(0);

            mShader->disable();
        }

        Terrain()
        {
            makePipelineShader("src/shaders/raymarch.vs", "src/shaders/terrain.fs");
        }

    };

} }

#endif // TERRAIN_H

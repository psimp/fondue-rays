#ifndef TERRAINVOXELIZER_H
#define TERRAINVOXELIZER_H

#include "renderpass.h"

namespace Fondue { namespace graphics {


    class TerrainVoxelizer : public RenderPass<EMPTY, EMPTY>
    {

        GLuint gPointBuffer, gPointMapTex, gVoxelMap, captureFBO, captureRBO, gTIDMapTex, gTIDBuffer, gIndexBuffer, gIndexMapTex, gHitMap;
        GLuint mFrameQuadVAO = rprims.getQuad();

    public:

        __attribute__((always_inline)) void gensdf()
        {
            glActiveTexture(GL_TEXTURE0 + TERRAIN_SDF);
            glGenTextures(1, &gVoxelMap);
            glBindTexture(GL_TEXTURE_3D, gVoxelMap);
            glTexImage3D(GL_TEXTURE_3D, 0, GL_R16F, TERRAIN_GRID_SIZE, TERRAIN_GRID_SIZE/4, TERRAIN_GRID_SIZE, 0, GL_RED, GL_HALF_FLOAT, 0);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_BORDER);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_BORDER);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_WRAP_R, GL_CLAMP_TO_BORDER);

            glGenFramebuffers(1, &captureFBO);
            glBindFramebuffer(GL_FRAMEBUFFER, captureFBO);
            glFramebufferTexture3D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_3D, gVoxelMap, 0, 0);
            const GLenum buffers[] = { GL_COLOR_ATTACHMENT0 };
            glDrawBuffers(1, buffers);
            glReadBuffer(GL_NONE);
            glBindFramebuffer(GL_FRAMEBUFFER, 0);

            mShader->enable();
            mShader->setUniform1i("gridSize",  TERRAIN_GRID_SIZE);
            mShader->setUniform1i("grassDecal",  CH0_MAP);
            mShader->setUniform1i("tubeDecal",   CH3_MAP);

            glBindFramebuffer(GL_FRAMEBUFFER, captureFBO);
            glViewport(0, 0, TERRAIN_GRID_SIZE, TERRAIN_GRID_SIZE/4);

                glBindVertexArray(mFrameQuadVAO);

                for (unsigned int i = 0; i < TERRAIN_GRID_SIZE; ++i) {
                    mShader->setUniform1u("layer", i);
                    glFramebufferTextureLayer(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, gVoxelMap, 0, i);
                    glDrawArrays(GL_TRIANGLE_STRIP, 0, 4);
                }

            glViewport(0, 0, SCREEN_WIDTH, SCREEN_HEIGHT);
            glBindFramebuffer(GL_FRAMEBUFFER, 0);

            mShader->disable();
        }

        TerrainVoxelizer()
        {
            makePipelineShader("src/shaders/voxelizer.vs", "src/shaders/voxelizeTerrain.fs");
        }


    };

} }

#endif // VOXELIZER_H

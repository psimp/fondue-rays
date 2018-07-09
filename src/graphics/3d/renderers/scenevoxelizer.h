#ifndef SCENEVOXELIZER_H
#define SCENEVOXELIZER_H

#include "renderpass.h"

namespace Fondue { namespace graphics {

    class SceneVoxelizer : public RenderPass<EMPTY, EMPTY>
    {

        GLuint gPointBuffer, gPointMapTex, gVoxelMap, captureFBO, captureRBO, gTIDMapTex, gTIDBuffer, gIndexBuffer, gIndexMapTex, gHitMap;
        GLuint mFrameQuadVAO = rprims.getQuad();

    public:

        __attribute__((always_inline)) void gensdf()
        {
            glActiveTexture(GL_TEXTURE0 + SCENE_SDF);
            glGenTextures(1, &gVoxelMap);
            glBindTexture(GL_TEXTURE_3D, gVoxelMap);
            glTexImage3D(GL_TEXTURE_3D, 0, GL_R16F, SCENE_GRID_SIZE, SCENE_GRID_SIZE, SCENE_GRID_SIZE, 0, GL_RED, GL_HALF_FLOAT, 0);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_WRAP_R, GL_CLAMP_TO_EDGE);

            glActiveTexture(GL_TEXTURE0 + SCENE_HITTRACKER);
            glGenTextures(1, &gHitMap);
            glBindTexture(GL_TEXTURE_3D, gHitMap);
            glTexImage3D(GL_TEXTURE_3D, 0, GL_RG16F, SCENE_GRID_SIZE/4, SCENE_GRID_SIZE/4, SCENE_GRID_SIZE/4, 0, GL_RG, GL_HALF_FLOAT, 0);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_MAG_FILTER, GL_NEAREST);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
            glTexParameteri(GL_TEXTURE_3D, GL_TEXTURE_WRAP_R, GL_CLAMP_TO_EDGE);

            glGenFramebuffers(1, &captureFBO);
            glBindFramebuffer(GL_FRAMEBUFFER, captureFBO);
            glFramebufferTexture3D(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_TEXTURE_3D, gVoxelMap, 0, 0);
            const GLenum buffers[] = { GL_COLOR_ATTACHMENT0 };
            glDrawBuffers(1, buffers);
            glReadBuffer(GL_NONE);
            glBindFramebuffer(GL_FRAMEBUFFER, 0);

            mShader->enable();
            mShader->setUniformBlockBinding("BoundingBoxes", VIEWSPACE_BBOX_UBO_BINDING);
            mShader->setUniformBlockBinding("Orientations", VIEWSPACE_ORIENTATION_UBO_BINDING);
            mShader->setUniform1i("modelSdf",  VOXEL_MAP);
            mShader->setUniform1i("numBoundingBoxes",   View::getNumBBoxes());
            mShader->setUniform1i("hittracking",  0);
            mShader->setUniform1i("gridSize",  SCENE_GRID_SIZE);

            glBindFramebuffer(GL_FRAMEBUFFER, captureFBO);
            glViewport(0, 0, SCENE_GRID_SIZE, SCENE_GRID_SIZE);
                glBindVertexArray(mFrameQuadVAO);

                for (unsigned int i = 0; i < SCENE_GRID_SIZE; ++i) {
                    mShader->setUniform1u("layer", i);
                    glFramebufferTextureLayer(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, gVoxelMap, 0, i);
                    glDrawArrays(GL_TRIANGLE_STRIP, 0, 4);
                }

            mShader->setUniform1i("hittracking",  1);
            mShader->setUniform1i("gridSize",  SCENE_GRID_SIZE/4);

            glViewport(0, 0, SCENE_GRID_SIZE/4, SCENE_GRID_SIZE/4);

                for (unsigned int i = 0; i < SCENE_GRID_SIZE/4; ++i) {
                    mShader->setUniform1u("layer", i);
                    glFramebufferTextureLayer(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, gHitMap, 0, i);
                    glDrawArrays(GL_TRIANGLE_STRIP, 0, 4);
                }

            glBindVertexArray(0);
            glViewport(0, 0, SCREEN_WIDTH, SCREEN_HEIGHT);
            glBindFramebuffer(GL_FRAMEBUFFER, 0);

            mShader->disable();
        }

        SceneVoxelizer()
        {
            makePipelineShader("src/shaders/voxelizer.vs", "src/shaders/voxelizer.fs");
        }


    };

} }

#endif // VOXELIZER_H

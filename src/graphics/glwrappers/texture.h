#ifndef TEXTURE_H
#define TEXTURE_H

#include <glad/glad.h>

#include <string>

#include "FreeImage/FreeImage.h"
#include "../../utils/imageloader.h"

namespace Fondue { namespace graphics {

    class Texture
    {

    private:

        GLuint mTID, mBinding;
        size_t mWidth, mHeight;

    public:

        Texture(const BYTE* data, size_t width, size_t height, GLuint binding, GLenum edgeCase = GL_CLAMP_TO_EDGE, GLenum filtering = GL_LINEAR);
        Texture(const std::string& filename, GLuint binding, GLenum edgeCase = GL_CLAMP_TO_EDGE, GLenum filtering = GL_LINEAR);
        ~Texture();
        void bind(GLuint binding);
        void unbind() const;

        void generateMipMaps() const;
        void save_binaries(const std::string &fileOut) const;

    public:

        inline GLuint getTID() const { return mTID; }

    };

} }

#endif // TEXTURE_H

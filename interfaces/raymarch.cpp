#include "../src/protoengine.h"

using namespace Fondue;
using namespace graphics;

class Rays : public Engine<Rays>
{

private:

    TexturedBatch mesh{22};

    ModelVoxelizer voxelizer;

    Texture ch0{"res/o1.jpg", CH0_MAP, GL_REPEAT, GL_LINEAR_MIPMAP_LINEAR};
    Texture ch1{"res/rgbnoise.jpg", CH1_MAP, GL_REPEAT, GL_LINEAR_MIPMAP_LINEAR};
    Texture ch3{"res/rgbnoise.jpg", CH3_MAP, GL_REPEAT, GL_LINEAR_MIPMAP_LINEAR};

    Camera camera{maths::vec3(0.0f, 0.0f, 0.0f), maths::mat4::perspective(45.0f, SCREEN_WIDTH / SCREEN_HEIGHT, 0.1f, 10000.0f)};

    Raytracer renderer;

    bool GUIEnabled = false;

    mesh_s s0, s1;

public:

    Rays()
    { }

    ~Rays()
	{

	}

    void init()
    {
        View::init_viewspace_bbox_ubos();

        ch0.generateMipMaps();
        ch1.generateMipMaps();
        ch3.generateMipMaps();

        model_importer::load_model("res/models/tiger/tiger.obj", nullptr, nullptr, &mesh, &voxelizer);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,0,0, 1.0f);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,0,0, 1.0f);
        entity_manager::abs_rotate(s0, 90, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,0,0, 1.0f);
        entity_manager::abs_rotate(s0, 180, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,0,0, 1.0f);
        entity_manager::abs_rotate(s0, 270, 0, 1, 0);


        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,100,0, 1.0f);
        entity_manager::abs_rotate(s0, 10, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,100,0, 1.0f);
        entity_manager::abs_rotate(s0, 70, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,100,0, 1.0f);
        entity_manager::abs_rotate(s0, 130, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,100,0, 1.0f);
        entity_manager::abs_rotate(s0, 190, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,100,0, 1.0f);
        entity_manager::abs_rotate(s0, 250, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,100,0, 1.0f);
        entity_manager::abs_rotate(s0, 310, 0, 1, 0);


        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,200,0, 1.0f);
        entity_manager::abs_rotate(s0, 20, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,200,0, 1.0f);
        entity_manager::abs_rotate(s0, 50, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,200,0, 1.0f);
        entity_manager::abs_rotate(s0, 80, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,200,0, 1.0f);
        entity_manager::abs_rotate(s0, 110, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,200,0, 1.0f);
        entity_manager::abs_rotate(s0, 140, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,200,0, 1.0f);
        entity_manager::abs_rotate(s0, 170, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,200,0, 1.0f);
        entity_manager::abs_rotate(s0, 200, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,200,0, 1.0f);
        entity_manager::abs_rotate(s0, 230, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,200,0, 1.0f);
        entity_manager::abs_rotate(s0, 260, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,200,0, 1.0f);
        entity_manager::abs_rotate(s0, 290, 0, 1, 0);

        s0 = mesh.add_instance();
        entity_manager::place_entity(s0, 0,200,0, 1.0f);
        entity_manager::abs_rotate(s0, 350, 0, 1, 0);


        mWindow.captureCursor();
        View::updateViewSpace();
	}

    void init_GeometryDependant()
    {
        renderer.init_GeometryDependant();
    }

    void tick()
    {
        std::cout << getFPS() << std::endl;
    }

    void update()
    {
        camera.updateView();

        if (mWindow.isKeyPressed(GLFW_KEY_W))
            camera.translate(FORWARD, getFrameTime());
        if (mWindow.isKeyPressed(GLFW_KEY_S))
            camera.translate(BACKWARD, getFrameTime());
        if (mWindow.isKeyPressed(GLFW_KEY_A))
            camera.translate(RIGHT, getFrameTime());
        if (mWindow.isKeyPressed(GLFW_KEY_D))
            camera.translate(LEFT, getFrameTime());
        if (mWindow.isKeyPressed(GLFW_KEY_Z))
            camera.translate(UP, getFrameTime());
        if (mWindow.isKeyPressed(GLFW_KEY_X))
            camera.translate(DOWN, getFrameTime());

        auto mouseMovement = mWindow.getMouseMovement();
        if (!GUIEnabled && mouseMovement.first != 0 && mouseMovement.second != 0.0f)
            camera.rotate(-mouseMovement.first, -mouseMovement.second);

        if (mWindow.isKeyTyped(GLFW_KEY_G))
        {
            mWindow.toggleCursor();
            GUIEnabled = !GUIEnabled;
        }

        //View::updateViewSpace();
	}

    void render()
    {
        renderer.draw(getTimeElapsed());
	}

    Raytracer getRenderer() const;
    void setRenderer(const Raytracer &value);
};

int main()
{
    Rays i;
    i.start();
    return 0;
}

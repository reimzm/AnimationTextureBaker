# Animation Texture Baker 

Bake vertex data into texture2d images via compute shader, then use a shader interpolate between vertex position to animate the mesh verticies 

![Check out this MP4](https://raw.githubusercontent.com/nukadelic/AnimationTextureBaker/master/Img%7E/Unity_TnP5YSO6ol.mp4)

## Installation 

Edit the `manifest.json` file located in the `Packages` folder of your unity project and
add the follwing line to the list of `dependencies`:
```json
"com.nukadelic.animationtexturebaker": "https://github.com/nukadelic/AnimationTextureBaker.git"
```

## How to bake :
* Drag and drop fbx to empty scene ( not necessary but i prefer it )
* Add animation component to prefab
* Add [ AnimationBaker ] component to the game object , freshly added component will have the option to automatically scan for all animations that the fbx is linked to  - or you can manually add them to the clips list inside the component
* Attach compute shader assest "MeshInfoTextureGen"  to the InfoTexGen filed on the component
* Use any shader that supports vertex animations , or use the demo one : "AnimationBaker_Example" in the root of this plugin folder
* Finally press Bake button 

## Settings & Info 
* The baked assets will be saved inside the target folder ( "Save To Folder" field on the component ) and inside that folder it will create a subfolder with the same name as the current active game object. Same applies to any generated assets , the game object name will be the prefix for all the names , keep that in mind if you like to keep your stuff organized
* Frame resolution will reflect on how many animations keyframes the compute shader will sample for generating the animation texture , the larger the value , the bigger is the height of the output textures ( position texture , normals textures , and tangents textures )
* The output texture width will depend on the mesh vertex count - so low poly models will have very small texture ( in width )
* If you want to bake only one animation remove other clips and keep only 1 clip in the component "clips" list
* Collapse mesh checkbox will basically designed to protect your 3d models if someone would to reverse engineer the build application
    

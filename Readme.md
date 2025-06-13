# Animation Texture Baker 





Bake vertex data into texture2d images via compute shader, then use a shader interpolate between vertex position to animate the mesh verticies 

https://github.com/nukadelic/AnimationTextureBaker/assets/6582633/8f15c948-c2ab-439a-bff6-60b94e9046c0

## Update

Add the dependencies for the shader Graph and URP, as well as create a new Lit Play Shader based on them.
https://github.com/user-attachments/assets/99398ce4-b232-418d-8ba0-13bf60745d0f

## Installation 

Edit the `manifest.json` file located in the `Packages` folder of your unity project and
add the follwing line to the list of `dependencies`:
```json
"com.nukadelic.animationtexturebaker": "https://github.com/reimzm/AnimationTextureBaker.git"
```

## How to bake :
* Drag and drop fbx to empty scene ( not necessary but i prefer it )
* Add animation component to prefab
* Add [ AnimationBaker ] component to the game object , freshly added component will have the option to automatically scan for all animations that the fbx is linked to  - or you can manually add them to the clips list inside the component
* Attach compute shader assest "MeshInfoTextureGen"  to the InfoTexGen filed on the component
* Use any shader that supports vertex animations , or use the demo one : "AnimationBaker_Example" in the root of this plugin folder
* Finally press Bake button 
( see 'Horse Bake Example' prefabs for reference ) 

## Settings & Info 
* The baked assets will be saved inside the target folder ( "Save To Folder" field on the component ) and inside that folder it will create a subfolder with the same name as the current active game object. Same applies to any generated assets , the game object name will be the prefix for all the names , keep that in mind if you like to keep your stuff organized
* Frame resolution will reflect on how many animations keyframes the compute shader will sample for generating the animation texture , the larger the value , the bigger is the height of the output textures ( position texture , normals textures , and tangents textures )
* The output texture width will depend on the mesh vertex count - so low poly models will have very small texture ( in width )
* If you want to bake only one animation remove other clips and keep only 1 clip in the component "clips" list
* Collapse mesh checkbox will basically designed to protect your 3d models if someone would to reverse engineer the build application
    
## The vertex shader

![image](https://github.com/nukadelic/AnimationTextureBaker/assets/6582633/1c3077cb-ac49-49f3-8177-fad51406a3c2)


## [Update] Added combined bake option 
* bake all animations in a single texture
* Custom shader for playback
* Generated scriptable object with each animation frame time data
* Example Mono player ( when baked with gneerated prefab option , will auto attach the demo script )

https://github.com/nukadelic/AnimationTextureBaker/assets/6582633/857aa5a4-979c-4d82-a6cc-3697427c74bd


# Importacao glTF 2.0

`GltfImporter` carrega `.gltf` e `.glb` diretamente para recursos Nova3D. A
importacao deve acontecer na thread dona do `GraphicsDevice`.

```csharp
using var model = new GltfImporter(GraphicsDevice).Load("Models/building.glb");

foreach (GltfInstance instance in model.Instances)
{
    GltfPrimitive primitive = model.Primitives[instance.PrimitiveIndex];
    primitive.Mesh.Draw(GraphicsDevice);
}
```

Para renderizar a cena importada diretamente pelo pipeline PBR:

```csharp
using var renderer = new GltfModelRenderer(model, pbrEffect, sun, environment);
renderer.Transform = Matrix.CreateScale(2f) * Matrix.CreateTranslation(position);
renderer.Draw(renderContext);

// Dentro de cada cascade do CSM:
renderer.DrawShadows(renderContext, shadowEffect, lightViewProjection);
```

O renderer aplica os transforms dos nos, fatores base-color/metallic/roughness,
materiais double-sided e registra draws/triangulos no `RenderStatistics`. Ele
nao assume ownership de `GltfModel`; o asset manager ou o chamador continuam
responsaveis por seu lifetime.

O modelo preserva transforms da hierarquia, instancias de meshes e indices de
material. `GltfModel.Dispose()` libera meshes e texturas importadas.

Suporte atual:

- glTF 2.x JSON e GLB 2.0;
- buffers externos, embutidos em GLB e data URI base64;
- buffer views intercaladas (`byteStride`);
- indices unsigned de 8, 16 e 32 bits;
- POSITION, NORMAL e TEXCOORD_0;
- geracao de normais quando NORMAL nao existe;
- multiplas primitivas e instancias por hierarquia de nos;
- fatores metallic-roughness, cor base, alpha e double-sided;
- texturas base-color, normal, metallic-roughness e occlusion;
- imagens externas, data URI e imagens em buffer views.

Ainda fora deste primeiro corte: accessors sparse, morph targets, skinning,
animacoes, Draco/Meshopt e extensoes de materiais. Primitivas diferentes de
`TRIANGLES` falham explicitamente em vez de produzir geometria incorreta.

O renderer conecta as texturas base-color, normal, metallic-roughness e
occlusion ao shader PBR. Normal mapping reconstrói a base tangente por derivadas
de posição/UV no pixel shader, preservando o orçamento de interpoladores usado
pelo CSM. Neste corte, apenas `TEXCOORD_0` é suportado e alpha `BLEND` ainda é
renderizado como opaco.

Para reload versionado:

```csharp
AssetHandle<GltfModel> building = assets.LoadGltf(
    "building", "Models/building.glb", GraphicsDevice);
```

GLB e o formato recomendado para hot reload. Em `.gltf`, uma alteracao apenas
no `.bin` ou na imagem externa nao muda a data do arquivo principal e, por
isso, nao dispara o watcher atual.

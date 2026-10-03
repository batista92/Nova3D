# Asset management e shader hot reload

`FileAssetManager` mantem assets de arquivo em handles versionados. O polling e
o carregamento acontecem na thread do jogo; portanto, a criacao e a destruicao
de recursos graficos continuam na thread dona do `GraphicsDevice`.

O reload e transacional: primeiro o novo recurso e criado. O handle so e
atualizado se isso terminar com sucesso; em caso de erro, a versao anterior
permanece ativa e uma nova tentativa ocorre depois.

```csharp
AssetHandle<Effect> terrain = assets.LoadEffect(
    "terrain", "Shaders/Compiled/Terrain.mgfxo", GraphicsDevice);

Effect current = terrain.Value;
int version = terrain.Version;
```

Materiais aceitam um provider, portanto passam a usar a nova versao do shader
no frame seguinte sem serem reconstruidos:

```csharp
AssetHandle<Effect> shader = assets.LoadEffect(
    "pbr", "Shaders/Compiled/PBR.mgfxo", GraphicsDevice);

var material = new PbrMaterial(
    "building", () => shader.Value, sun, environment);
```

O helper recebe bytecode `.mgfxo` bruto. Os efeitos atuais do benchmark ainda
sao produzidos e carregados como XNB pelo `ContentManager`; eles nao entram
automaticamente nesse fluxo. Materiais e o renderer glTF ja aceitam providers
versionados. Para ativar reload no benchmark, o build de desenvolvimento ainda
precisa expor os `.mgfxo` recompilados.

Essa separacao e deliberada: o sistema nao observa arquivos `.fx` fingindo que
eles podem ser entregues diretamente ao driver, nem descarta um shader valido
quando uma recompilacao falha.

## Assets referenced by scene documents

Scene documents store portable paths relative to a configured `Assets` root.
Use `SceneAssetResolver` to contain and verify the path, then
`SceneAssetCache<T>` to share explicitly owned runtime values. For glTF models,
`SceneModelComponentDescriptor.CreateCached` connects the resolver/cache to the
scene runtime without making core Nova3D own a global asset service.

The cache is graphics-thread-affine because its callbacks may create or destroy
MonoGame resources. A `SceneAssetLease<T>` is one reference, not the asset
itself. Dispose scene instances before the cache. Cancellation is cooperative:
the cache checks before and after loading, destroys a value created just before
cancellation, and never publishes a partially acquired entry.

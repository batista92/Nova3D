**uma ferramenta 3D reutilizável sobre MonoGame**, não é uma "engine”. O city builder será o primeiro cliente e, se a camada se provar boa, extraímos o núcleo para os próximos jogos.

MonoGame é adequado como fundação porque já nos entrega o framework .NET, pipeline gráfico programável, buffers, render targets, effects/shaders, input, áudio e multiplataforma. Custom effects são textuais e podem ser compilados pelo Content Pipeline ou diretamente pelo MGFXC. ([GitHub][1])

Está dividido em **duas fases grandes: primeiro provar que vale a pena; só depois construir a ferramenta.**

## Estado do projeto — 25/09/2026

```text
Fase 1 — Provas de conceito    CONCLUÍDA
Gate #1 — Terrain Triplanar    APROVADO
Gate #2 — City Benchmark       APROVADO
MonoGame                       APROVADO
Fase 2 — Nova3D M1             CONCLUÍDO
Fase 2 — Nova3D M2             CONCLUÍDO
Fase 2 — Nova3D M3             CONCLUÍDO
Fase 2 — Nova3D M4             CONCLUÍDO
Fase 3 — Física opcional       CONCLUÍDA
Fase 4 — UI opcional           CONCLUÍDA
Fase 5 — AI Developer Experience CONCLUÍDA
Fase 6 — Release v0.2.0        CONCLUÍDA
```

Os testes demonstraram que MonoGame fornece uma base 3D adequada quando o
renderer necessário é construído explicitamente sobre ele. As limitações
encontradas ficaram sob nosso controle: shaders, samplers, atlas, shadows,
culling, LOD e organização dos passes puderam ser diagnosticados e corrigidos
sem depender de uma camada fechada de engine.

# Fase 1 — Provas de conceito

O objetivo aqui é **tentar matar a ideia rapidamente**. Não vamos construir arquitetura bonita antes de sabermos que MonoGame consegue entregar o que precisamos.

### Teste 1 — PBR

Primeiro precisamos eliminar a principal preocupação: “3D do monogame é ruim”.

Criar uma cena extremamente pequena:

```text
Camera
DirectionalLight

Sphere
├── metal
├── plástico
└── material áspero

Plane
└── concreto
```

Implementar em HLSL:

```text
PBR
├── Albedo
├── Normal
├── Metallic
├── Roughness
├── AO
│
├── Cook-Torrance
├── GGX
├── Smith Geometry
├── Schlick Fresnel
├── Normal mapping
└── Gamma correction

```

Inicialmente nem precisamos de sistema de materiais sofisticado.

**Critério de aprovação:** conseguir algo visualmente comparável a um material PBR básico do Godot/Unity.

---

# Teste 2 — iluminação e sombras

Depois:

```text
Directional Light
       │
       ▼
 Cascaded Shadow Maps
       │
       ├── Cascade 0
       ├── Cascade 1
       ├── Cascade 2
       └── Cascade 3
```

Testar:

* PCF;
* bias;
* shadow acne;
* peter-panning;
* estabilidade das cascades;
* objetos instanciados projetando sombras.

Esse teste é importante para o city builder porque teremos câmera alta e **distâncias enormes de visualização**.

---

# Teste 3 — o assassino: Terrain Shader

Aqui repetimos exatamente o teste que matou o Stride.

```text
Terrain
│
├── Grass
│   ├── Albedo
│   ├── Normal
│   └── Roughness
│
├── Dirt
├── Rock
└── Sand

        ↓

Triplanar Mapping
+
Normal Mapping
+
Height Blend
+
Slope Blend
```

Nada de sistema de material proprietário.

Queremos literalmente:

```text
Terrain.fx
```

recebendo texturas e parâmetros.

MonoGame oficialmente suporta custom `Effect`/MGFX para esse tipo de pipeline programável. ([MonoGame Docs][2])

**Se esse teste der problema estrutural → paramos aqui.**

Esse é nosso Gate #1.

---

# Teste 4 — Terrain runtime

Se shader passar:

```text
Terrain
 ↓
Chunks 64x64
 ↓
VertexBuffer individual
 ↓
Heightmap
```

Precisamos conseguir fazer:

```text
Mouse
 ↓
Brush
 ↓
alterar heights
 ↓
atualizar chunk
 ↓
recalcular normals
 ↓
atualizar GPU
```

E somente os chunks afetados devem ser atualizados.

Testar também:

* raise/lower;
* flatten;
* smooth;
* atualização de normais;
* bounds;
* picking;
* atualização de colisão futuramente.

---

# Teste 5 — Vegetação

Agora repetimos o teste que o Stride passou.

```text
10.000 árvores
```

Mas não:

```text
10.000 DrawModel()
```

Queremos:

```text
Tree Mesh
   +
InstanceBuffer
   ↓
GPU Instancing
   ↓
10.000 árvores
```

Depois:

```text
LOD 0 → 0–50 m
LOD 1 → 50–150 m
LOD 2 → 150–400 m
Cull  → 400+ m
```

Billboard fica opcional.

---

# Teste 6 — mundo grande

Agora começa o teste realmente relacionado ao city builder.

Por exemplo:

```text
2048 × 2048 terrain

dividido em

32 × 32 chunks
```

Adicionar:

```text
Frustum Culling
Chunk LOD
Vegetation Culling
Object Culling
```

E movimentar rapidamente a câmera pelo mapa.

O objetivo não é apenas FPS.

Mediremos:

```text
FPS
CPU frame time
GPU frame time
RAM
VRAM
Draw calls
Triangles
Visible chunks
Visible instances
```

MonoGame fornece render targets e acesso suficientemente baixo ao pipeline para construirmos as passagens necessárias; não estamos presos ao `BasicEffect`. ([MonoGame Docs][3])

---

# Teste 7 — Post Processing

Só agora fazemos a cena ficar realmente bonita.

Começaria com:

```text
HDR
 ↓
Exposure
 ↓
ACES Tone Mapping
 ↓
Bloom
 ↓
FXAA
```

Depois podemos experimentar:

```text
SSAO
Fog
Color grading
TAA
```

Não colocaria tudo no primeiro renderer.

---

# Teste 8 — cena do city builder

Esse é o **Gate #2**.

Montamos uma cena fake:

```text
Grande Terrain

10.000 árvores

1.000 prédios

500 veículos fake

água

sombras

PBR

terrain triplanar

LOD

fog

post processing
```

Não existe gameplay.

É exclusivamente um benchmark.

Se tivermos:

**visual satisfatório + performance satisfatória + IA conseguindo manter os shaders**, encerramos a fase de pesquisa.

A partir daí: **MonoGame está aprovado.**

## Baseline congelada — CityBenchmark

O Teste 08 deixa de ser código descartável e passa a ser o benchmark permanente
de regressão do renderer.

```text
Build                 Release
Backend               DesktopGL
Resolução             1920 × 1080
VSync                  off
Terrain                2048 × 2048
Terrain chunks         32 × 32 (1024)
Árvores                10.000
Prédios                1.000
Veículos               500
CSM                    4 × 4096²
Água                   shader próprio
PostFX                 HDR + exposure + ACES + bloom + FXAA

Baseline observada
Frame                  3,31 ms
FPS                    302
Draw calls             748
Triângulos visíveis    61k
```

O valor de frame acima é o intervalo observado do frame completo, não uma
medição isolada de GPU. Hardware, driver e resolução devem acompanhar futuras
medições. Quando o profiler estiver disponível, a baseline será dividida em:

```text
CPU update
CPU render
GPU frame
shadow pass
terrain pass
geometry pass
water pass
post-processing
```

Toda mudança relevante no renderer deve executar novamente o CityBenchmark.
Regressões precisam ser explicadas e registradas antes de serem aceitas.

---

# Fase 2 — Nova3D

Aqui eu daria até um nome temporário, por exemplo:

```text
CityBuilder
     │
     ▼
   Nova3D
     │
     ▼
  MonoGame
```

O objetivo do `Nova3D` não seria competir com Godot.

Seria uma **camada 3D code-first extremamente amigável para IA**.

Definição de produto:

> **Nova3D — 3D rendering/world toolkit for MonoGame.**

Nova3D não substitui MonoGame e, neste estágio, não será tratada como uma
engine. MonoGame continua responsável por plataforma, game loop, input, áudio,
gráficos e tipos matemáticos fundamentais.

A filosofia seria:

> Poucas abstrações, código C#, shaders HLSL explícitos e zero magia.

## Regra arquitetural: não esconder MonoGame

Não criaremos wrappers que apenas renomeiam tipos já claros:

```text
Não criar                 Continuar usando
NovaGraphicsDevice       GraphicsDevice
NovaTexture              Texture2D
NovaVector3              Vector3
NovaMatrix               Matrix
NovaVertexBuffer         VertexBuffer
```

Nova3D abstrai os conceitos que pertencem à nossa ferramenta:

```text
Terrain
TerrainMaterial
PbrMaterial
Mesh
DirectionalLight
ShadowSystem
VegetationSystem
WorldRenderer
PostProcessPipeline
```

Isso mantém a API pequena, explícita e compreensível tanto por pessoas quanto
por IA.

## Marcos de execução

### M1 — Fundação

```text
Core
Resources
Shader management
Camera
Mesh
Material
RenderContext
RenderStatistics
```

O CityBenchmark foi movido para `Benchmarks/CityBenchmark` e consome essas APIs
sem perder comportamento ou desempenho.

### M2 — Renderer

```text
PBR
Lighting
CSM
Instancing
LOD
Culling
HDR pipeline
PostFX
Water
```

### M3 — World

```text
Terrain
Terrain chunks
Runtime deformation
Terrain materials
Vegetation
Spatial partition
World streaming
```

### M4 — Production

```text
glTF/GLB
Asset management
Hot reload de shaders
Debug rendering
Profiler
Configuration
Logging
```

Estado atual:

```text
Asset management       CONCLUÍDO (handles versionados e reload transacional)
Hot reload de shaders  CONCLUÍDO (reload transacional + materiais versionados)
Debug rendering        CONCLUÍDO
Profiler               CONCLUÍDO
Configuration          CONCLUÍDO
Logging                CONCLUÍDO
glTF/GLB                CONCLUÍDO (importador + texturas PBR + shadow pass)
```

Sistemas específicos do city builder só serão extraídos depois desses quatro
marcos. A ferramenta permanece dentro do projeto enquanto as fronteiras ainda
estiverem sendo comprovadas.

Validação final do M4: 37 arquivos GLB reais carregados e renderizados com
materiais PBR, texturas e sombras; zero falhas de importação.

---

## 1. Core

Começamos pequeno:

```text
Nova3D.Core

Game
Time
Graphics
Input
Logging
Configuration
```

MonoGame continua responsável pela plataforma.

---

## 2. Renderer

Essa será a parte principal.

```text
Nova3D.Rendering

Renderer
│
├── Camera
├── Mesh
├── Material
├── Texture
├── Shader
├── RenderTarget
│
├── Lighting
│   ├── DirectionalLight
│   ├── PointLight
│   └── SpotLight
│
├── Shadows
│   └── CascadedShadowMap
│
├── Instancing
│
└── PostProcessing
```

E quero uma API simples.

Algo conceitualmente como:

```csharp
Material material = new PbrMaterial
{
    Albedo = textures.Load("grass_albedo"),
    Normal = textures.Load("grass_normal"),
    Roughness = textures.Load("grass_roughness")
};

renderer.Draw(mesh, material, transform);
```

A IA entende isso imediatamente.

---

# 3. Shader Library

Essa parte eu considero **fundamental para o projeto**.

```text
Shaders/
│
├── Common/
│   ├── Math.fxh
│   ├── Lighting.fxh
│   ├── PBR.fxh
│   ├── Shadows.fxh
│   └── Triplanar.fxh
│
├── PBR.fx
├── Terrain.fx
├── Vegetation.fx
├── Water.fx
├── Sky.fx
└── PostProcess.fx
```

Nada escondido.

Se daqui a seis meses perguntarmos para a IA:

> “Adicione wind animation na vegetação.”

ela abre:

```text
Vegetation.fx
```

e trabalha.

Não precisa descobrir como nossa engine gera shaders.

---

# 4. Materials

Criaria um sistema pequeno:

```text
Material
   │
   ├── PbrMaterial
   ├── TerrainMaterial
   ├── VegetationMaterial
   └── UnlitMaterial
```

Não faria Shader Graph.

Não faria sistema de composição automática.

**Shader explícito > magia.**

Foi exatamente essa camada que nos causou problemas no Stride.

---

# 5. Terrain

Aqui teremos provavelmente nosso primeiro módulo realmente grande:

```text
Nova3D.Terrain

Terrain
├── TerrainChunk
├── TerrainMesh
├── HeightMap
├── TerrainMaterial
│
├── TerrainLOD
├── TerrainCulling
│
├── TerrainRaycaster
│
└── TerrainEditor
    ├── Raise
    ├── Lower
    ├── Flatten
    ├── Smooth
    └── Paint
```

E desde o início:

```text
Terrain != Renderer
```

O terrain produz dados para o renderer.

Isso vai facilitar muito manutenção.

---

# 6. Vegetation

Depois:

```text
Nova3D.Vegetation

VegetationSystem
├── InstanceBatch
├── LOD
├── Culling
├── Distribution
└── Wind
```

Árvores, pedras, arbustos, postes etc.

O city builder vai abusar disso.

---

# 7. World Rendering

Precisaremos controlar o que realmente chega à GPU.

```text
World
 │
 ├── Spatial Partition
 │
 ├── Frustum Culling
 │
 ├── Distance Culling
 │
 ├── LOD
 │
 └── Render Queue
        │
        ├── Opaque
        ├── AlphaTest
        ├── Transparent
        └── Instanced
```

Provavelmente começaria com grid/quadtree, não algo super sofisticado.

---

# 8. Asset layer

Não quero construir Blender 2.0.

Precisamos somente de:

```text
TextureLoader
ModelLoader
ShaderLoader
MaterialLoader
```

E escolher **glTF/GLB como formato 3D principal**.

Internamente podemos converter/otimizar posteriormente.

---

# 9. Debug Tools

Isso é especialmente importante porque **a IA estará desenvolvendo conosco**.

Quero poder apertar uma tecla e ver:

```text
FPS:              121
Frame:            8.2 ms

CPU:              3.1 ms
GPU:              4.7 ms

Draw calls:       284
Triangles:        1.8M
Instances:        12,482

Terrain chunks:   38/1024
Shadow draws:     91

VRAM:             ...
```

Além disso:

```text
wireframe
bounding boxes
chunk boundaries
LOD colors
shadow cascades
normals
light bounds
```

Isso torna o trabalho da IA muito mais objetivo:

> "LOD1 está aparecendo vermelho muito perto da câmera."

é muito melhor que:

> "parece que está meio lento."

---

# 10. Física

Eu **não faria física**.

Usaria biblioteca externa.

Nossa camada apenas faria integração:

```text
Nova3D.Physics
       │
       ▼
Physics Adapter
       │
       ▼
biblioteca externa
```

O mesmo vale para áudio.

Não existe motivo para escrever essas partes.

---

# Arquitetura final

Eu imagino algo assim:

```text
             CITY BUILDER
                  │
     ┌────────────┼────────────┐
     │            │            │
 Simulation      Roads      Buildings
     │
     │
     ├────────── Terrain
     │
     └────────── Vegetation
                  │
                  ▼
              ┌────────┐
              │ Nova3D │
              └────────┘
                  │
       ┌──────────┼───────────┐
       │          │           │
    Renderer    Assets      Physics
       │
       ├── PBR
       ├── HLSL
       ├── Shadows
       ├── Terrain
       ├── Instancing
       └── PostFX
                  │
                  ▼
              MonoGame
                  │
                  ▼
                 GPU
```

E existe uma decisão arquitetural que eu considero **muito importante**:

### Não separar a ferramenta do jogo imediatamente.

Começaria:

```text
CityBuilder/
├── Engine/
│   ├── Rendering/
│   ├── Terrain/
│   ├── Vegetation/
│   └── ...
│
└── Game/
    ├── Simulation/
    ├── Buildings/
    ├── Roads/
    └── ...
```

Conforme sistemas ficarem claramente genéricos:

```text
Engine/
    ↓
Nova3D/
```

Isso evita passarmos seis meses construindo uma engine imaginária para jogos que ainda não existem.

**O city builder dita as necessidades da ferramenta, não o contrário.**

---

## FASE 3 — FÍSICA OPCIONAL

```text
P1 — Fundação                                      CONCLUÍDO
   pacote Nova3D.Physics.Bepu
   fixed timestep e multithreading
   box, sphere, capsule e static box
   raycast e ownership de shapes
   benchmark headless com 1.000 corpos

P2 — Diagnóstico e filtros                         CONCLUÍDO
   collision layers e masks
   materiais físicos configuráveis
   debug draw de colliders
   estatísticas da simulação

P3 — Terrain physics                               CONCLUÍDO
   collider por terrain chunk
   streaming cria/remove collider junto do chunk
   deformação reconstrói somente chunks afetados
   teste de alinhamento visual/físico

P4 — Gameplay foundations                          CONCLUÍDO
   kinematic bodies e triggers
   sweep/shape cast
   character-controller sample
   constraints essenciais

P5 — Physics Gate                                  APROVADO
   cena visual interativa                           VALIDADA
   stress test e alocações por frame                AUTOMATIZADO
   validação de determinismo local                  AUTOMATIZADO
   documentação e template opcional                 VALIDADO
```

Regras do gate: `Nova3D` não depende de BEPU; física usa passo fixo; chunks
visuais e físicos compartilham a mesma fonte de altura; e recursos avançados
continuam acessíveis por `BepuPhysicsWorld.Simulation` sem duplicar a API BEPU.

---

## FASE 4 — UI OPCIONAL

```text
U1 — Spike Gum                                      APROVADO
   compatibilidade com MonoGame 3.8.4.1             VALIDADO
   inicialização, update e draw                     VALIDADO
   redimensionamento da janela                      VALIDADO
   mouse, teclado e gamepad                         VALIDADO
   medição de tempo e alocações                     VALIDADO

U2 — Fundação                                       CONCLUÍDO
   pacote Nova3D.UI.Gum                             IMPLEMENTADO
   lifecycle explícito                             IMPLEMENTADO
   escala virtual e DPI                            IMPLEMENTADO
   ordem de renderização                           IMPLEMENTADO
   captura de input                                IMPLEMENTADO
   ownership e dispose                             IMPLEMENTADO

U3 — Navegação                                      CONCLUÍDO
   UiScreen                                         IMPLEMENTADO
   screen stack                                     IMPLEMENTADO
   menu principal e pause menu                     IMPLEMENTADO
   modal/dialog                                     IMPLEMENTADO
   hooks de transição                               IMPLEMENTADO

U4 — HUD                                            CONCLUÍDO
   labels e barras                                  IMPLEMENTADO
   atualização sem recriar controles                IMPLEMENTADO
   ancoragem responsiva                             IMPLEMENTADO
   world-to-screen markers                          IMPLEMENTADO
   notificações                                     IMPLEMENTADO

U5 — Produção                                       CONCLUÍDO
   temas e fontes                                   VALIDADO
   navegação por gamepad                            VALIDADO
   acessibilidade básica                            VALIDADO
   template --ui                                    VALIDADO
   documentação para IA                             CONCLUÍDO
   UI benchmark e gate                              APROVADO
```

Regras: `Nova3D` não depende de Gum; a integração permanece opcional; tipos e
controles Gum não serão duplicados por wrappers Nova3D; gameplay controla o
conteúdo e as ações da UI; e o spike deve medir custo antes da extração do módulo.

---

## FASE 5 — AI DEVELOPER EXPERIENCE

Objetivo: permitir que um agente sem histórico do CityBuilder crie, valide e
publique um jogo Nova3D completo carregando apenas o contexto necessário. Mais
documentação não é sucesso por si só; o gate mede autonomia, correção e custo de
contexto.

```text
A1 — Contexto mínimo                                 CONCLUÍDO
   AI_QUICKSTART.md com caminho feliz               IMPLEMENTADO
   mapa de documentos por tarefa                    IMPLEMENTADO
   limites e ownership em formato compacto          IMPLEMENTADO
   AGENTS.md como roteador, não manual completo      IMPLEMENTADO

A2 — Receitas operacionais                           CONCLUÍDO
   formato padrão: quando/arquivos/código/validação  IMPLEMENTADO
   criar jogo e lifecycle                            IMPLEMENTADO
   GLB, material e cena                              IMPLEMENTADO
   corpo físico, esfera e câmera follow              IMPLEMENTADO
   menu, HUD, timer e game flow                      IMPLEMENTADO
   áudio, settings, saves e publicação               IMPLEMENTADO

A3 — Catálogo da API                                 CONCLUÍDO
   API_INDEX.md por tipo público                     IMPLEMENTADO
   propósito, ownership e dispose                    IMPLEMENTADO
   módulo/pacote e link para receita                 IMPLEMENTADO
   limites v0.2 próximos da API afetada              IMPLEMENTADO

A4 — Validação de um comando                         CONCLUÍDO
   eng/validate.ps1 para o toolkit                   IMPLEMENTADO
   validação de jogo gerado                          IMPLEMENTADO
   build, MGCB, testes, packages e templates         IMPLEMENTADO
   saída curta, determinística e amigável para IA    IMPLEMENTADO

A5 — Samples compiláveis                             CONCLUÍDO
   Minimal3D                                         IMPLEMENTADO
   PhysicsPlayground                                 IMPLEMENTADO
   GumMenus                                          IMPLEMENTADO
   RollingBall vertical slice                        IMPLEMENTADO
   build dos samples como regressão                  IMPLEMENTADO

A6 — Pacote para agentes                             CONCLUÍDO
   skill nova3d-game-development                     IMPLEMENTADO
   roteamento progressivo de contexto                IMPLEMENTADO
   comandos e protocolo de diagnóstico               IMPLEMENTADO
   modelo de avaliação de problemas                  IMPLEMENTADO

A7 — Presets e AI Gate                               CONCLUÍDO
   avaliar preset arcade3d com evidência dos samples IMPLEMENTADO (ADIADO)
   projeto Marble3D criado em repositório externo    IMPLEMENTADO
   catálogo real de problemas Nova3D                 IMPLEMENTADO (N3D-001)
   build executável validado                         APROVADO
   menu, áudio, física, checkpoint e vitória         VALIDADOS MANUALMENTE
   relatório de autonomia e contexto                 IMPLEMENTADO
```

### Restrições da Fase 5

- Receitas não podem esconder MonoGame nem duplicar suas APIs.
- Código específico do Marble3D permanece no repositório do jogo.
- Um preset só será criado depois que dois samples demonstrarem repetição real.
- Snippets críticos devem existir também em sample compilável ou teste.
- Documentos longos não entram no contexto inicial; o agente os abre por tópico.
- A skill roteia fontes existentes e scripts; não copia todo o manual.
- Problemas devem ser reproduzidos antes de alterar Nova3D.

### Métricas do AI Gate

```text
Contexto inicial
   somente AGENTS.md + AI_QUICKSTART.md
   AI_QUICKSTART alvo: até 250 linhas
   receita alvo: até 150 linhas por tarefa

Autonomia
   agente novo cria o projeto sem histórico do CityBuilder
   build Release e validação executados por um único comando
   nenhuma cópia de código interno da Nova3D para o jogo

Correção
   ownership e ordem de lifecycle preservados
   física e UI continuam opcionais
   problemas registrados com reprodução e evidência

Entrega
   menu, fase jogável, checkpoint, timer, áudio, settings,
   vitória/derrota e executável do Marble3D
```

O Gate #5 foi aprovado em 2026-09-25. Um agente em contexto novo entregou o
Marble3D usando os pacotes públicos/locais e a documentação distribuída pelo
template; build, publicação e startup foram validados automaticamente, e menu,
áudio, física, checkpoint e vitória foram validados manualmente. Intervenções,
contexto consumido e problemas encontrados permanecem registrados para orientar
a próxima versão da Nova3D.

Débito visual não bloqueante: a geometria verde do checkpoint aparece sobre a
parte inferior da esfera durante a sobreposição. O owner permanece `Unknown`
até existir uma reprodução mínima; não está classificado como defeito Nova3D.

---

## FASE 6 — RELEASE v0.2.0

Objetivo: publicar uma revisão reproduzível da Nova3D contendo o toolkit base,
os módulos opcionais de física e UI, o template e a documentação validada pelo
Marble3D.

```text
R1 — Versão e documentação                           CONCLUÍDO
   quatro pacotes alinhados em 0.2.0                 IMPLEMENTADO
   template referencia pacotes 0.2.0                IMPLEMENTADO
   instalador independente de versão fixa           IMPLEMENTADO
   documentação e checklist normalizados            IMPLEMENTADO

R2 — Gerar pacotes                                   CONCLUÍDO
   Nova3D 0.2.0                                      APROVADO
   Nova3D.Physics.Bepu 0.2.0                         APROVADO
   Nova3D.UI.Gum 0.2.0                               APROVADO
   Nova3D.Templates 0.2.0                            APROVADO

R3 — Validar template isolado                        CONCLUÍDO
   instalação em custom hive                         APROVADO
   variante padrão                                   APROVADO
   variante --physics                                APROVADO
   variante --ui                                     APROVADO
   variante --physics --ui                           APROVADO

R4 — Validar consumidor externo                      CONCLUÍDO
   Marble3D consumindo somente pacotes 0.2.0         APROVADO
   publish self-contained win-x64                    APROVADO
   smoke do executável publicado                     APROVADO

R5 — Regressões e benchmark                          CONCLUÍDO
   build Release e MGCB                              APROVADO
   regressão de física e samples                     APROVADO
   CityBuilder build/MGCB; performance Gate #2      PRESERVADA
   validate.ps1 completo                             25 ETAPAS APROVADAS

R6 — Auditar conteúdo dos .nupkg                     CONCLUÍDO
   conteúdo dos quatro .nupkg                        APROVADO
   dependências e metadados                          APROVADO
   ausência de bin/obj e assets acidentais           APROVADO
   auditoria integrada ao validate.ps1               IMPLEMENTADO

R7 — Tag e GitHub Release                            CONCLUÍDO
   confirmar IDs no NuGet                            APROVADO
   criar commit e reconstruir pacotes                APROVADO
   criar tag v0.2.0 no commit dos pacotes            APROVADO
   criar GitHub Release e anexar artefatos           APROVADO
```

A tag `v0.1.0` permanece no marco original do toolkit. Ela não será movida; os
módulos opcionais e a experiência para agentes formam a versão `v0.2.0`.

---

## FASE 7 — GAME AUTHORING / Nova3D v0.3

Objetivo: reduzir a quantidade de código e de contexto necessária para uma IA
transformar assets e uma descrição de gameplay em um jogo 3D completo. A fase
não busca criar um editor visual, uma linguagem de scripts ou um ECS genérico.
O foco é um caminho textual, determinístico, validável e compatível com código
C# escrito pelo jogo.

### Princípios da fase

- MonoGame continua visível; tipos matemáticos, gráficos, áudio e input não são
  renomeados apenas para criar uma segunda API.
- Cenas descrevem composição e configuração. Regras de gameplay continuam no
  projeto do jogo.
- Todo formato persistido possui versão explícita e diagnóstico com caminho do
  arquivo, objeto e propriedade que falhou.
- Parsing e validação devem funcionar sem `GraphicsDevice`; criação e descarte
  de recursos GPU continuam na graphics thread.
- Física, UI e futuros módulos permanecem opcionais. O core conhece somente
  contratos de extensão, nunca implementações dos módulos opcionais.
- Uma nova abstração só é aprovada com documentação curta, sample compilável,
  regressão automatizada e evidência de redução de código/contexto para IA.
- Arquivos inválidos falham claramente. Campos ou componentes desconhecidos
  não serão ignorados silenciosamente.

```text
G1 — Scene document e validação                       CONCLUÍDO
G2 — Scene runtime e prefabs                          CONCLUÍDO
G3 — Game flow e carregamento                         CONCLUÍDO
G4 — Input actions e remapeamento                     PLANEJADO
G5 — glTF skinning e animation                        PLANEJADO
G6 — Áudio e persistência reutilizável                PLANEJADO
G7 — Nova3D CLI                                       PLANEJADO
G8 — Regressão visual e diagnósticos                  PLANEJADO
G9 — Segundo AI Gate                                  PLANEJADO
G10 — Release v0.3.0                                  PLANEJADO
```

### G1 — Scene document e validação

Objetivo: definir um formato declarativo pequeno antes de implementar seu
runtime. Este marco não renderiza nem instancia física.

```text
G1.1 — Contrato do formato                           CONCLUÍDO
   identificador e versão do schema                  IMPLEMENTADO
   convenção de coordenadas e unidades               IMPLEMENTADO
   IDs estáveis, nomes e hierarquia                   IMPLEMENTADO
   transform local: position/rotation/scale           IMPLEMENTADO
   referências relativas e portáveis                 IMPLEMENTADO

G1.2 — Modelo e parser CPU-only                      CONCLUÍDO
   documentos de scene, node e component             IMPLEMENTADO
   leitura/escrita com System.Text.Json               IMPLEMENTADO
   preservação de ordem determinística                IMPLEMENTADO
   erro contendo arquivo e JSON path                  IMPLEMENTADO

G1.3 — Validação                                     CONCLUÍDO
   versão suportada                                   IMPLEMENTADO
   IDs duplicados e referências ausentes              IMPLEMENTADO
   ciclos na hierarquia                               IMPLEMENTADO
   transforms e valores numéricos inválidos           IMPLEMENTADO
   tipo de componente desconhecido                    IMPLEMENTADO
   validação agregada sem esconder erros seguintes    IMPLEMENTADO

G1.4 — Contrato para extensão                        CONCLUÍDO
   registry explícito de component descriptors        IMPLEMENTADO
   nenhum scan mágico de assemblies                   IMPLEMENTADO
   módulos opcionais registrados pelo consumidor      IMPLEMENTADO
   API pública e ownership documentados               IMPLEMENTADO

G1.5 — Evidência                                     CONCLUÍDO
   fixtures válidas e inválidas                       IMPLEMENTADO (PARSER + VALIDATOR)
   testes sem janela ou GPU                           IMPLEMENTADO (PARSER + VALIDATOR)
   JSON Schema distribuído com docs/pacote            IMPLEMENTADO
   receita curta para agentes                         IMPLEMENTADO
```

Critério de aceite: um agente consegue gerar uma cena, validá-la e corrigir um
erro usando apenas o diagnóstico produzido, sem abrir a implementação da
Nova3D. O schema inicial suportará somente hierarquia, transforms e componentes
registrados; não haverá herança, expressões ou scripts embutidos.

### G2 — Scene runtime e prefabs

Objetivo: transformar um documento válido em uma instância com lifecycle e
ownership previsíveis.

```text
G2.1 — Instanciação em duas fases                    CONCLUÍDO
   parse/validate no lado CPU                         IMPLEMENTADO
   resolução/criação GPU na graphics thread           IMPLEMENTADO
   rollback sem vazamento em falha parcial            IMPLEMENTADO
   SceneInstance descartável                         IMPLEMENTADO

G2.2 — Componentes v0.3                              CONCLUÍDO
   model/renderable GLB                               IMPLEMENTADO
   camera                                             IMPLEMENTADO
   directional light                                  IMPLEMENTADO
   ponto de spawn/tag                                 IMPLEMENTADO
   extensões de física via registry                   IMPLEMENTADO

G2.3 — Assets                                         CONCLUÍDO
   resolução pelo Assets root + contexto da cena       IMPLEMENTADO
   cache e ownership explícitos                       IMPLEMENTADO
   asset ausente com diagnóstico acionável            IMPLEMENTADO
   cancelamento e descarte seguro                      IMPLEMENTADO

G2.4 — Prefabs                                       CONCLUÍDO
   formato baseado no mesmo schema de nodes           IMPLEMENTADO
   instanciação múltipla com IDs isolados             IMPLEMENTADO
   overrides explícitos e tipados                     IMPLEMENTADO
   detecção de referência recursiva                    IMPLEMENTADO

G2.5 — Ferramentas e sample                          CONCLUÍDO
   debug bounds/names por node                        IMPLEMENTADO
   sample DataDrivenScene                             IMPLEMENTADO
   template com cena mínima opcional                  IMPLEMENTADO
```

Gate #6 — Scene Authoring: um nível externo deve ser montado com JSON + assets,
conter ao menos dois prefabs reutilizados e abrir sem código específico na
Nova3D. Um arquivo inválido deve falhar antes de alocar recursos GPU. O sample e
o benchmark atual continuam compilando e sem regressão material.

### G3 — Game flow e carregamento

Objetivo: padronizar troca de fases sem colocar regras de gameplay no toolkit.

```text
G3.1 — Scene service
   load, activate, unload e reload                    CONCLUÍDO
   somente uma transição mutável por vez              CONCLUÍDO
   cancelamento e recuperação da cena anterior        CONCLUÍDO
   relatório de progresso sem depender de UI          CONCLUÍDO

G3.2 — Estados reutilizáveis
   boot, loading, playing, paused e result             CONCLUÍDO
   hooks controlados pelo jogo                        CONCLUÍDO
   restart da fase atual                              CONCLUÍDO
   retorno ao menu sem recursos órfãos                 CONCLUÍDO

G3.3 — Integração
   UI observa estado, mas não o possui                 CONCLUÍDO
   física pausa/resume por adaptador opcional          CONCLUÍDO
   lifecycle documentado no quickstart                CONCLUÍDO
   regressão de load/unload repetido                  CONCLUÍDO
```

Critério de aceite: alternar repetidamente entre menu e duas fases não aumenta
recursos vivos, não deixa corpos físicos antigos e não exige referência de
`Nova3D` para `Nova3D.UI.Gum` ou `Nova3D.Physics.Bepu`.

### G4 — Input actions e remapeamento

Objetivo: separar intenção de gameplay do dispositivo sem esconder os estados
MonoGame quando acesso de baixo nível for necessário.

```text
G4.1 — Action map
   ações digitais, eixos 1D e eixos 2D                CONCLUÍDO
   keyboard, mouse e gamepad                          CONCLUÍDO
   pressed, released, down e value                    CONCLUÍDO
   deadzone, scale e inversão                         CONCLUÍDO

G4.2 — Contextos
   gameplay, menu e debug                             CONCLUÍDO
   prioridade e ativação explícitas                   CONCLUÍDO
   integração com captura de input do Gum             CONCLUÍDO
   sem input atravessando modal/pause                 CONCLUÍDO

G4.3 — Remapeamento
   bindings serializáveis                             CONCLUÍDO
   detecção de conflito                               CONCLUÍDO
   defaults recuperáveis                              CONCLUÍDO
   receita de tela de controles                       CONCLUÍDO

G4.4 — Validação
   testes de transição determinísticos                CONCLUÍDO
   teclado e gamepad no sample                        CONCLUÍDO
   zero alocação por ação no hot path                 CONCLUÍDO
```

Critério de aceite: o mesmo gameplay funciona com teclado e gamepad, pode ser
remapeado e persiste os bindings sem conhecer teclas dentro da lógica da fase.

### G5 — glTF skinning e animation

Objetivo: remover a principal limitação para jogos com personagens. A primeira
etapa é uma prova medida; o transporte da palette para GPU não será escolhido
sem validar limites reais no perfil HiDef.

```text
G5.1 — Spike e limites
   modelos reais de referência                        CONCLUÍDO
   limite de joints medido/documentado                CONCLUÍDO
   estratégia de palette comparada                    CONCLUÍDO
   custo CPU/GPU e sampler budget registrados         CONCLUÍDO

G5.2 — Importação glTF
   JOINTS_0 e WEIGHTS_0                               CONCLUÍDO
   skins e inverse bind matrices                      CONCLUÍDO
   animation samplers e channels                      CONCLUÍDO
   translation, rotation e scale                      CONCLUÍDO
   interpolação LINEAR e STEP                         CONCLUÍDO

G5.3 — Runtime
   skeleton pose                                      CONCLUÍDO
   clip player: play, loop, speed e stop              CONCLUÍDO
   blend simples entre dois clips                     CONCLUÍDO
   atualização sem alocação por joint/frame           CONCLUÍDO

G5.4 — Rendering e debug
   shader de skinning                                 CONCLUÍDO
   bounds corretos para pose animada                  CONCLUÍDO
   skeleton/bones debug                               CONCLUÍDO
   personagem animado no sample                       CONCLUÍDO
```

Fora do primeiro corte: IK, retargeting, animação facial, morph targets, root
motion automático e compressão avançada. Esses itens só entram após uso real.

Gate #7 — Animated Character: um GLB externo deve carregar, reproduzir idle e
walk, alternar clips sem vazamentos e manter bounds/culling corretos. O gate
registra número de joints, custo de update, draw calls e limitações do asset.

### G6 — Áudio e persistência reutilizável

Objetivo: extrair apenas comportamento repetido, preservando `SoundEffect`,
`Song` e `MediaPlayer` na API do jogo.

```text
G6.1 — Áudio
   master/music/SFX buses                             CONCLUÍDO
   volume, mute, fade e loop                          CONCLUÍDO
   pool limitado de SoundEffectInstance               CONCLUÍDO
   emitter/listener 3D                               CONCLUÍDO
   lifecycle e perda de foco                         CONCLUÍDO

G6.2 — Persistência
   diretório correto por plataforma                   CONCLUÍDO
   JSON versionado                                    CONCLUÍDO
   escrita atômica e fallback                         CONCLUÍDO
   settings, bindings e save slots                    CONCLUÍDO
   hook explícito para migração                       CONCLUÍDO

G6.3 — Decisão de empacotamento
   medir repetição em dois jogos                      CONCLUÍDO
   decidir core versus módulo opcional                CONCLUÍDO
   impedir dependência de gameplay                    CONCLUÍDO
```

Critério de aceite: configurações corrompidas recuperam defaults com log claro;
volume e bindings sobrevivem ao restart; saves usam diretório de usuário e não
o diretório do executável.

### G7 — Nova3D CLI

Objetivo: disponibilizar validação e diagnóstico sem exigir checkout do
repositório Nova3D.

```text
G7.1 — Produto e distribuição
   projeto dotnet tool separado                      CONCLUÍDO
   pacote e versão alinhados                         CONCLUÍDO
   instalação local e via NuGet                      CONCLUÍDO

G7.2 — Comandos
   nova3d doctor                                     CONCLUÍDO
   nova3d validate                                   CONCLUÍDO
   nova3d inspect <model.glb>                        CONCLUÍDO
   nova3d inspect <scene.json>                       CONCLUÍDO
   nova3d publish --runtime <RID>                    CONCLUÍDO

G7.3 — Contrato para agentes
   exit codes estáveis                               CONCLUÍDO
   saída humana curta                                CONCLUÍDO
   opção --format json                               CONCLUÍDO
   nenhuma pergunta interativa em CI                 CONCLUÍDO
   erros com ação recomendada                        CONCLUÍDO
```

Em G7.1, "via NuGet" significa que o `DotnetTool` produzido é instalado e
testado pelo fluxo padrão `dotnet tool install` usando o feed local isolado. A
publicação desse mesmo pacote em nuget.org permanece uma ação de release em
G10.3.

Critério de aceite: um projeto gerado e isolado consegue diagnosticar ambiente,
validar assets/cenas e produzir um executável usando somente o SDK, pacotes e a
CLI distribuídos.

### G8 — Regressão visual e diagnósticos

Objetivo: detectar automaticamente regressões que compilação e FPS não revelam.

```text
G8.1 — Captura determinística
   câmera, resolução, seed e timestep fixos           CONCLUÍDO
   captura após warm-up conhecido                     CONCLUÍDO
   metadata de GPU/backend junto da imagem            CONCLUÍDO

G8.2 — Comparação
   baseline versionada por cena                       CONCLUÍDO
   tolerância configurável                            CONCLUÍDO
   diff visual e métricas                             CONCLUÍDO
   atualização de baseline explícita                  CONCLUÍDO

G8.3 — Cobertura inicial
   PBR/material                                       CONCLUÍDO
   CSM                                                CONCLUÍDO
   terrain/vegetation                                 CONCLUÍDO
   água/post-FX                                       CONCLUÍDO
   GLB estático e animado                             CONCLUÍDO

G8.4 — Performance
   orçamento de frame, draws e triângulos             CONCLUÍDO
   regressão física                                   PRESERVADA
   relatório legível por IA                          CONCLUÍDO
```

Comparações entre GPUs diferentes serão evidência auxiliar, não igualdade
pixel-perfect. O gate oficial usa ambiente controlado e tolerância registrada.

### G9 — Segundo AI Gate

Objetivo: provar generalidade com um jogo diferente de Marble3D e do benchmark
de cidade. O projeto permanece em repositório externo e consome somente pacotes
públicos/locais documentados.

Brief mínimo do jogo de validação:

```text
personagem GLB animado
controle em terceira pessoa
duas cenas carregáveis
prefabs reutilizados
obstáculos ou inimigos simples
input teclado + gamepad remapeável
menu, pause e HUD
áudio e settings persistentes
objetivo, vitória e derrota
save/progresso mínimo
publish executável
```

Evidência obrigatória:

- agente começa somente com `AGENTS.md`, `AI_QUICKSTART.md` e o brief;
- registrar documentos abertos, intervenções humanas, comandos e falhas;
- nenhuma cópia de fonte interno da Nova3D para o jogo;
- cenas e prefabs representam o level, não código procedural equivalente;
- build, validação, publish e smoke executados por comandos reproduzíveis;
- issues classificadas pelo template de avaliação antes de alterar a toolkit;
- comparar linhas de código e contexto consumido com o Gate #5.

Gate #8 — AI Game Production: aprovado quando um agente novo entrega o brief,
com no máximo correções humanas de requisitos/arte, sem mudanças ad hoc na
Nova3D e com redução mensurável de código/contexto contra o Marble3D.

O GateGame revelou uma lacuna distinta da capacidade técnica: o menu e as
configurações seguem um layout genérico, volume é alterado por botões de 25% e
as células de coleta ficam estáticas. Marble3D mostrou o mesmo problema nos
menus, embora suas gemas já tenham animação simples.
Esses jogos são evidência para o próximo marco; build, FPS e funcionalidade não
atestam qualidade visual ou clareza para o jogador.

### G9.1 — Qualidade de apresentação dos jogos por IA

Este marco melhora o contrato de criação dos próximos jogos, mantendo tema e
comportamento específicos em cada repositório. Marble3D e GateGame são casos
de diagnóstico; não serão retrabalhados como produtos. O guia está em
`Docs/Recipes/game-presentation.md` e é distribuído com o template.

```text
G9.1a — Brief e direção visual
   brief GAME_DESIGN.md distribuído no template                         CONCLUÍDO
   agente inspeciona assets antes de compor menus                        CONTRATO
   direção visual própria de cada jogo documentada                      CONTRATO

G9.1b — Menus, HUD e settings
   menus com hierarquia, foco e identidade do jogo                       CONTRATO
   volume contínuo com slider e valor visível; mute com toggle          CONTRATO
   remapeamento com cancelar, conflito e restaurar padrões              CONTRATO
   mouse, teclado, gamepad, resize e persistência                         CONTRATO

G9.1c — Feedback e movimento de objetos
   pickups com movimento sutil, destaque e coleta                       CONTRATO
   hazards, checkpoint, saída, vitória e derrota com sinais legíveis     CONTRATO
   preferência de movimento reduzido respeitada                          CONTRATO
   sem alocações por objeto a cada frame                                  CONTRATO

G9.1d — Validação por evidência
   PRESENTATION_REVIEW.md distribuído no template                        CONCLUÍDO
   capturas de menu, settings, pausa, HUD e resultado                    CONTRATO
   revisão em resolução alvo e janela menor                              CONTRATO
   teste real de slider, foco, Back, áudio e coleta                      CONTRATO
   validação em dois jogos novos, com estilos diferentes                 PENDENTE
```

Critério de aceite: agentes recebem briefs diferentes e os documentos distribuídos,
entregam dois jogos novos com menus legíveis e distintos, controles apropriados
e feedback visível para objetos interativos, e registram capturas e observações.
Uma inspeção humana das telas e do movimento continua necessária; o prompt
orienta decisões, mas não garante qualidade por si só. Esta revisão complementa
os critérios técnicos do Gate #8 e deve ocorrer antes da release seguinte.

### G10 — Release v0.3.0

```text
G10.1 — Compatibilidade e documentação
   API index e quickstart atualizados                 IMPLEMENTADO
   schemas e formatos versionados                     DOCUMENTADO (v1 preservado)
   breaking changes e migração documentados           IMPLEMENTADO

G10.2 — Automação
   CI Windows                                         APROVADO NO COMMIT 018f53d
   CI Linux para build/test aplicável                 APROVADO NO COMMIT 018f53d
   solution, samples, shaders e templates             VALIDAÇÃO LOCAL APROVADA
   auditoria dos pacotes preservada                   IMPLEMENTADO

G10.3 — Distribuição
   IDs e ownership no NuGet confirmados               ADIADO POR DECISÃO (FEED LOCAL)
   pacotes assinados/verificáveis, se aplicável       SHA-256 VERIFICADO; NUGET ADIADO
   pré-flight reprodutível de release                 APROVADO NO COMMIT 018f53d
   template e CLI na GitHub Release                   CONCLUÍDO (PACOTES LOCAIS)
   template e CLI no nuget.org                        ADIADO POR DECISÃO (FEED LOCAL)
   tag e GitHub Release                               CONCLUÍDO (v0.3.0)
```

### Ordem de implementação

```text
G1 Scene document
 └─► G2 Scene runtime/prefabs ─► G3 Game flow
             │                         │
             └─────────────────────────┼─► G9 Segundo AI Gate
G4 Input actions ──────────────────────┤
G5 Animation ──────────────────────────┤
G6 Audio/persistence ──────────────────┤
G7 CLI ────────────────────────────────┤
G8 Visual regression ──────────────────┘
                                      │
                                      └─► G9.1 Apresentação ─► G10 Release v0.3.0
```

G8 começa de forma incremental durante G1 e cresce com cada marco; não deve ser
adiado integralmente para o fim. G5 pode avançar em paralelo conceitualmente,
mas sua API pública só é estabilizada depois do spike de limits/performance.

### Primeiro incremento implementável

O início da fase será `G1.1–G1.3`, limitado a:

1. escrever a especificação `scene/1` e um exemplo mínimo;
2. implementar modelo, parser e validador CPU-only;
3. cobrir arquivos válidos, versão inválida, IDs duplicados, ciclos, referência
   ausente, componente desconhecido e números não finitos;
4. integrar a validação ao comando do repositório;
5. documentar como uma IA cria e corrige uma cena.

Não entram nesse incremento: `GraphicsDevice`, GLB, física, prefabs, hot reload,
game flow ou mudanças no template. Essa fronteira permite validar o contrato do
formato antes de acoplar recursos runtime.

## Roadmap resumido

```text
FASE 1 — VALIDAR                         CONCLUÍDA
│
├─ 01 PBR                                APROVADO
├─ 02 Shadows                            APROVADO
├─ 03 Terrain Triplanar     ← GATE #1    APROVADO
├─ 04 Terrain Runtime                    APROVADO
├─ 05 Instancing + LOD                   APROVADO
├─ 06 Large World                        APROVADO
├─ 07 Post Processing                    APROVADO
└─ 08 City Benchmark        ← GATE #2    APROVADO
             │
             ▼
       MONOGAME APROVADO
             │
             ▼
FASE 2 — NOVA3D
│
├─ M1 Fundação                           CONCLUÍDO
├─ M2 Renderer                           CONCLUÍDO
├─ M3 World                              CONCLUÍDO
└─ M4 Production                         CONCLUÍDO
             │
             ▼
          Nova3D v0.1                    CONCLUÍDA
             │
             ▼
FASE 3 — FÍSICA OPCIONAL                 APROVADA
│
└─ P1–P5 Nova3D.Physics.Bepu             CONCLUÍDO
             │
             ▼
FASE 4 — UI OPCIONAL                     APROVADA
│
└─ U1–U5 Nova3D.UI.Gum                   CONCLUÍDO
             │
             ▼
FASE 5 — AI DEVELOPER EXPERIENCE         CONCLUÍDA
│
└─ A1–A7 Marble3D              ← GATE #5 APROVADO
             │
             ▼
FASE 6 — RELEASE v0.2.0                   CONCLUÍDA
│
└─ R1–R7                         ← RELEASE v0.2.0 PUBLICADA
             │
             ▼
FASE 7 — GAME AUTHORING / v0.3             PLANEJADA
│
├─ G1–G2 Scenes e prefabs          ← GATE #6
├─ G3–G4 Flow e input
├─ G5 Animation                    ← GATE #7
├─ G6–G8 Produção, CLI e regressões
├─ G9 Segundo jogo por IA          ← GATE #8
├─ G9.1 Apresentação dos jogos      ← CONTRATO NO TEMPLATE; GATE EM JOGOS NOVOS PENDENTE
└─ G10 Release v0.3.0
```

Os gates anteriores validaram a base técnica, os módulos opcionais, a experiência
para agentes e a distribuição v0.2. A Fase 7 deve preservar esses gates enquanto
reduz o trabalho necessário para produzir jogos. Não criaremos ECS, editor visual
ou abstrações gerais sem evidência nos jogos. O scene system existe
especificamente para reduzir composição manual, código repetido e contexto
consumido por agentes.

E há um bônus interessante: o MonoGame continua suportando Windows, Linux e macOS, enquanto Vulkan/DX12 estão entrando como suporte preview na linha 3.8.5. Então podemos começar conservadoramente no backend estável e manter espaço para evoluir depois. ([GitHub][1])

[1]: https://github.com/MonoGame/MonoGame?utm_source=chatgpt.com "GitHub - MonoGame/MonoGame: One framework for creating powerful cross-platform games. · GitHub"
[2]: https://docs.monogame.net/articles/getting_started/content_pipeline/custom_effects.html?utm_source=chatgpt.com "Custom Effects | MonoGame"
[3]: https://docs.monogame.net/articles/getting_to_know/whatis/graphics/WhatIs_Render_Target.html?utm_source=chatgpt.com "What Is a Render Target? | MonoGame"

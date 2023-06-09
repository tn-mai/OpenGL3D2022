[OpenGL 3D 2023 Tips 第07回]

# タイルベースドレンダリング

## 習得目標

* 「タイル・ベースド・レンダリング(TBR)」の仕組みと利点について説明できる。
* 視錐台の交差判定方法について説明できる。
* ライトの影響半径を制限する必要性を説明できる。

## 1. 空間分割

### 1.1 全てのライトが見た目に影響するとは限らない

大量のライトを扱えるようになったのはいいのですが、そのぶん処理に時間がかかるようになりました。フラグメントを1つ描画するのに100個のライトを処理しなくてはならない以上、時間がかかるのは当然のことです。

しかし、ゲーム画面をよく観察してみると、全てのライトが全てのフラグメントに影響しているようには見えません。どちらかというと、近くにあるライト以外は何の影響もないように見えるのではないでしょうか。

その感覚は正しいものです。例えば、夜は星々や月の光が地上を照らしています。しかし昼間は星や月の光を感じることはありません。では、昼は、星や月の光は届いていないのでしょうか？

もちろん、そんなわけはありません。昼間でも間違いなくそれらの光は地上に届いています。しかし、太陽の光が強力すぎるため、人間の目では見分けられないのです。

つまり、「十分に弱い光は存在しないものとみなす」ことができるわけです。そこで、全てのライトに「影響範囲」というパラメータを追加し、フラグメントが範囲外にあるときはそのライトを無視するようにします。

ただ、影響範囲を設定しただけでは少ししか処理時間を減らせません。結局100個のライトについて、フラグメントが範囲内かどうかを調べなくてはならないからです。

こうした問題でよく使われるのは、空間を格子状に分割して、分割した小さな空間ごとに処理を分ける方法です。分割した空間に影響しないライトは描画処理から除外されるため、処理時間を減らすことができます。

<p align="center">
<img src="images/tips_07_tiled_light_culling.jpg" width="50%" /><br>
https://github.com/GPUOpen-LibrariesAndSDKs/ForwardPlus11
</p>

画面を分割したそれぞれの空間を「タイル」と呼びます。今回は画面を32x18個のタイルに分割し、個々の空間に影響するライトのリストを作ることにします。この分割数は、1920x1080と1280x720のどちらの画面サイズでも等分できる適度な数として選びました。

このように、画面をタイル状に分割して描画を行う技法は、`Tile Based Rendering`(タイル・ベースド・レンダリング), TBRと呼ばれています。

>**【Forward+(フォワード・プラス)について】**<br>
>本テキストの内容は、AMD社が2012年に発表した`Forward+`(フォワード・プラス)と呼ばれる技法を元にしています。ただし、元論文ではGPUのコンピュートシェーダで実行している計算を、CPUで実行するように再構成しています。元論文と実装は以下のURLにあります:<br>
>`https://github.com/GPUOpen-LibrariesAndSDKs/ForwardPlus11`

### 1.2 視錐台を定義する

画面に描画される領域は、カメラから見える範囲が錘台形(すいだいけい)をしていることから「視錐台(しすいだい)」と呼ばれます。今回のプログラムでは視錐台をタイルに分割し、それぞれのタイルについてライトの影響の有無を判定します。

<p align="center">
<img src="images/tips_07_tiled_frustum_3D.png" width="33%" />&emsp;<img src="images/tips_07_tiled_frustum_2D.png" width="44%" /><br>
[左=タイル化した視錐台&emsp;&emsp;右=タイル化した視錐台を上から見た図]
</p>

ところで、「視錐台をタイル状に分割ししてできる小さな視錐台」では呼びにくいので、今後は「サブ視錐台」と呼ぶことにします。そして、分割前の大きな視錐台は「メイン視錐台」と呼ぶことにします。

ポイントライトの影響範囲を球形とすると、サブ視錐台とライトを表す球が重なっていたら、そのライトはサブ視錐台に影響する可能性があります。

視錐台は6つの平面を組み合わせて作られます。これらは上下左右と前後の平面です。ただし、すべてのサブ視錐台の前後の面は、メイン視錐台の前後の面と同一です。

そのため、サブ視錐台に前後の面を持たせる必要はありません。また、判定をビュー空間で行うようにすれば、前後の平面は視点からの距離だけで判定できます。

これらのことから、上下左右の平面を持つ小さな「サブ視錐台」と、上下左右の平面に加えて前後方向の距離を持ち、全てのサブ視錐台を掌握する「メイン視錐台」の2つがあればよさそうです。

<p align="center">
<img src="images/tips_07_sub_frustum.png" width="40%" /><br>
https://www.3dgep.com/forward-plus/
</p>

さっそく視錐台を作成しましょう。メイン視錐台の構造体名は`Frusutm`(フラスタム、「視錐台」という意味)。サブ視錐台は`SubFrustum`(サブ・フラスタム)とします。

プロジェクトの`Src`フォルダに`Frustum.h`という名前のヘッダファイルを追加してください。追加したファイルを開き、次のプログラムを追加してください。

```diff
+/**
+* @file Frustum.h
+*/
+#ifndef FRUSTUM_H_INCLUDED
+#define FRUSTUM_H_INCLUDED
+#include "Collision.h"
+#include "VecMath.h"
+#include <memory>
+
+/**
+* 表示するライトを選別するためのサブ視錐台
+*/
+struct SubFrustum
+{
+  Collision::Plane planes[4]; // 0=左 1=右 2=上 3=下
+};
+
+/**
+* 表示するライトを選別するための視錐台
+*/
+struct Frustum
+{
+  // TBR用タイル情報(シェーダ側と一致させること)
+  static constexpr int tileCountX = 32; // 視錐台のX方向の分割数
+  static constexpr int tileCountY = 18; // 視錐台のY方向の分割数
+  static constexpr int lightsPerTile = 256; // 1タイルの想定ライト数
+
+  float zNear;     // メイン視錐台の手前側平面までの距離
+  float zFar;      // メイン視錐台の奥側平面までの距離
+  SubFrustum main; // メイン視錐台の上下左右平面
+  SubFrustum sub[tileCountY][tileCountX]; // サブ視錐台の配列
+};
+using FrustumPtr = std::shared_ptr<Frustum>;
+
+FrustumPtr CreateFrustum(
+  const VecMath::mat4& matInvProj, float zNear, float zFar);
+
+#endif // FRUSTUM_H_INCLUDED
```

>`Collision.h`に球と平面の構造体を定義していない場合は、以下の構造体定義を追加してください。
>
>```c++
>/**
>* 平面
>*/
>struct Plane
>{
>  VecMath::vec3 normal; // 面の法線
>  float d; // 原点からの距離
>};
>
>/**
>* 球体
>*/
>struct Sphere
>{
>  VecMath::vec3 p; // 中心座標
>  float radius;    // 半径
>};
>```

### 1.3 NDC座標をビュー座標に変換する

`CreateFrustum`関数を定義するためのファイルを追加しましょう。プロジェクトの`Src`フォルダに`Frustum.cpp`という名前のCPPファイルを追加してください。追加したファイルを開き、次のプログラムを追加してください。

```diff
+/**
+* @file Frustum.cpp
+*/
+#include "Frustum.h"
+
+using namespace Collision;
+using namespace VecMath;
```

`Frustum`オブジェクトを作成するにはサブフラスタムを作成する必要があります。サブフラスタムへの分割をビュー座標系で行うのは面倒なので、NDC座標系で分割してからビュー座標系に変換する、という手順を踏むことにします。

通常、シェーダではプロジェクション行列を使ってビュー座標系からクリップ座標系へ変換します。その後GPU内部で「`w`要素で除算」が行われ、NDC座標系に変換されます。

これと逆の操作を行えば、NDC座標系からビュー座標系へ変換することができます。手順は「NDC座標にプロジェクション行列の逆行列を掛け、その後`w`要素で除算する」となります。

逆プロジェクション行列を掛けると`w`要素には「クリップ座標系の`w`の逆数」が入るため、実質的に「クリップ座標系の`w`を掛ける」のと同じになるわけです。

それでは関数を定義しましょう。関数名は`NdcToView`(エヌディーシー・トゥ・ビュー)とします。`using`宣言の下に次のプログラムを追加してください。

```diff
 using namespace Collision;
 using namespace VecMath;
+
+/**
+* NDC座標をビュー座標に変換する
+*
+* @param p          NDC座標 
+* @param matInvProj 逆プロジェクション行列
+*
+* @return ビュー座標系に変換したpの座標
+*/
+vec3 NdcToView(const vec3& p, const mat4& matInvProj)
+{
+  const vec4 result = matInvProj * vec4(p, 1);
+  return vec3(result) / result.w;
+}
```

### 1.4 サブフラスタムを作成する

次に、`NdcToView`関数を使ってサブフラスタムを作る関数を書いていきます。関数名は`CreateSubFrustum`(クリエイト・サブフラスタム)でいいでしょう。

スクリーン座標からサブフラスタムを作成するには、逆プロジェクション行列とサブフラスタムの範囲を表す2つのスクリーン座標が必要です。

`Plane`構造体は「法線`normal`と平面までの距離`d`」によって平面を表します。ビュー座標系において視点は常に`(0, 0, 0)`にあります。サブ視錐台を構成する上下左右の平面は必ず視点を通るので、「平面までの距離」は常に`0`です。

法線は「外積」の結果が「2つのベクトルに垂直なベクトル」になる性質から計算できます。例えば左側の平面の場合「視点とサブ視錐台の左下を通るベクトル」と「視点と左上を通るベクトル」の外積が法線になります。

同様に、右の平面の法線は「視点と右上を通るベクトル」と「視点と右下を通るベクトル」の外積になります。それでは`NdcToView`関数の定義の下に、次のプログラムを追加してください。

```diff
   const vec4 result = matInvProj * vec4(p, 1);
   return vec3(result) / result.w;
 }
+
+/**
+* ビュー座標系のサブ視錐台を作成する
+*
+* @param matProj プロジェクション行列
+* @param x0      タイルの左下のX座標
+* @param y0      タイルの左下のY座標
+* @param x1      タイルの右上のX座標
+* @param y1      タイルの右上のY座標
+*
+* @return 作成したサブ視錐台
+*/
+SubFrustum CreateSubFrustum(const mat4& matInvProj,
+  float x0, float y0, float x1, float y1)
+{
+  const vec3 p0 = NdcToView(vec3(x0, y0, 1), matInvProj);
+  const vec3 p1 = NdcToView(vec3(x1, y0, 1), matInvProj);
+  const vec3 p2 = NdcToView(vec3(x1, y1, 1), matInvProj);
+  const vec3 p3 = NdcToView(vec3(x0, y1, 1), matInvProj);
+
+  return SubFrustum{
+    Plane{ normalize(cross(p0, p3)), 0 },
+    Plane{ normalize(cross(p2, p1)), 0 },
+    Plane{ normalize(cross(p3, p2)), 0 },
+    Plane{ normalize(cross(p1, p0)), 0 }
+  };
+}
```

### 1.5 メインフラスタムを作成する

`CreateFrustum`(クリエイト・フラスタム)関数は、引数として通常のプロジェクション行列と、手前及び奥の平面までの距離を受け取り、メインフラスタムとサブフラスタムを計算します。

サブフラスタムは、`CreateSubFrustum`関数を分割領域の数だけループさせて作成します。`CreateSubFrustum`関数の定義の下に、次のプログラムを追加してください。

```diff
     Plane{ normalize(cross(p1, p0)), 0 }
   };
 }
+
+/**
+* ビュー座標系の視錐台を作成する
+*
+* @param matProj プロジェクション行列
+* @param zNear   視点から近クリップ平面までの距離
+* @param zFar    視点から遠クリップ平面までの距離
+*
+* @return 作成した視錐台
+*/
+FrustumPtr CreateFrustum(const mat4& matProj, float zNear, float zFar)
+{
+  FrustumPtr frustum = std::make_shared<Frustum>();
+
+  // 逆プロジェクション行列を計算
+  const mat4& matInvProj = inverse(matProj);
+
+  // メインフラスタムを作成
+  frustum->zNear = -zNear;
+  frustum->zFar = -zFar;
+  frustum->main = CreateSubFrustum(matInvProj, -1, -1, 1, 1);
+
+  // サブフラスタムを作成
+  const vec2 tileCount(Frustum::tileCountX, Frustum::tileCountY);
+  for (int y = 0; y < Frustum::tileCountY; ++y) {
+    for (int x = 0; x < Frustum::tileCountX; ++x) {
+      const vec2 pos(static_cast<float>(x), static_cast<float>(y));
+      const vec2 min = pos / tileCount * 2 - 1;       // 左下の座標
+      const vec2 max = (pos + 1) / tileCount * 2 - 1; // 右上の座標
+      frustum->sub[y][x] =
+        CreateSubFrustum(matInvProj, min.x, min.y, max.x, max.y);
+    }
+  }
+  return frustum;
+}
```

`zNear`と`zFar`の符号を逆にしていることに注意してください。一般に近平面と遠平面の距離には正の値が使われます。しかしOpenGLのビュー座標系では視点の前方は-Z方向なので、座標の比較を簡単にするために負の値にしています。

また、サブフラスタムを計算するにはタイルの左下及び右上のNDC座標が必要となります。メインフラスタムを横に`tileCountX`個、縦に`tileCountY`個のタイルに分割したとき、変数`x`と`y`は分割したタイルの位置を表すインデックスに相当します。

サブフラスタムのNDC座標はこのインデックスから作成します。NDC座標系の範囲は`-1`～`+1`なので、まずインデックスをタイル数で割って`0`～`1`に変換し、次に`2`倍して`1`を引くと、`-1`～`+1`に変換できます。

### 1.6 平面と球の交差判定

視錐台は6つの平面を組み合わせて作られるため、6回の「球と平面の交差判定」で交差を判定できます。

<p align="center">
<img src="images/tips_07_sphere_inside_plane.png" width="33%" /><br>
</p>

まず交差判定関数を宣言します。さきほどは「球と平面の交差判定」と言いましたが、実際に必要なのは「球が平面のどちら側に存在するか」という判定です。球と平面の判定は以下の手順で行います。

>1. 球の中心座標から平面までの距離を計算する。
>2. 距離が球の半径未満なら交差している。
>3. 距離の値の符号が正なら、球の中心は表側(法線の向いている方向)にある。負なら裏側にある。

関数名は`SphereInsidePlane`(スフィア・インサイド・プレーン、「球は平面の表面側にありますか？」といった意味)としました。`CreateFrustum`関数の定義の下に、次のプログラムを追加してください。

```diff
   }
   return frustum;
 }
+
+/**
+* 球が平面の表側にあるかどうかを調べる
+*
+* @param sphere 球
+* @param plane  平面
+*
+* @retval true  平面の表側にあるか、部分的に重なっている
+* @retval false 完全に裏側にある
+*/
+bool SphereInsidePlane(
+  const Sphere& sphere, const Plane& plane)
+{
+  const float d = dot(plane.normal, sphere.p);
+  return plane.d - d <= sphere.radius;
+}
```

距離を求めるには内積を使います。内積によって中心座標を法線に射影することで、法線方向の原点からの距離`d`が求まります。

この`d`と平面の原点からの距離`plane.d`の差を求め、それが球の半径未満なら、球体は平面と交差している、または表側に存在します。

### 1.7 球とサブフラスタムの交差判定

次は球とサブフラスタムの交差判定を書いていきます。といっても、最も重要な「球と平面の交差判定」はすでに作ってあるので、難しい部分はありません。

球が、サブ視錐台の上下左右すべての面に対して内側に存在する、つまり`SphereInsidePlane`関数が`true`を返すなら交差しています。どれかひとつでも`false`だったら交差していません。

関数名は`SphereInsideSubFrustum`(スフィア・インサイド・サブフラスタム)とします。`SphereInsidePlane`関数の定義の下に次のプログラムを追加してください。

```diff
   const float d = dot(plane.normal, sphere.p);
   return plane.d - d <= sphere.radius;
 }
+
+/**
+* 球とサブ視錐台の交差判定
+*
+* @param sphere  球
+* @param frustum サブ視錐台
+*
+* @retval true  交差している
+* @retval false 交差していない
+*/
+bool SphereInsideSubFrustum(const Sphere& sphere, const SubFrustum& frustum)
+{
+  for (int i = 0; i < 4; ++i) {
+    if ( ! SphereInsidePlane(sphere, frustum.planes[i])) {
+      return false;
+    }
+  }
+  return true;
+}
```

### 1.8 球とメインフラスタムの交差判定

続いて、球とメインフラスタムの交差判定を書いていきます。メインフラスタムでは、まず前後の平面との交差判定を行います。これは、ビュー空間ではZ軸のみの判定になります。

球が前後の平面の内側にあることが分かったら、`SphereInsideSubFrustum`関数を使って上下左右の平面との交差判定を行います。

関数名は`SphereInsideFrustum`(スフィア・インサイド・フラスタム)とします。`SphereInsideSubFrustum`関数の定義の下に次のプログラムを追加してください。

```diff
   }
   return true;
 }
+
+/**
+* 球と視錐台の交差判定
+*
+* @param sphere  球
+* @param frustum 視錐台
+*
+* @retval true  交差している
+* @retval false 交差していない
+*/
+bool SphereInsideFrustum(const Sphere& sphere, const Frustum& frustum)
+{
+  if (sphere.p.z - sphere.radius > frustum.zNear) {
+    return false;
+  }
+  if (sphere.p.z + sphere.radius < frustum.zFar) {
+    return false;
+  }
+  return SphereInsideSubFrustum(sphere, frustum.main);
+}
```

最後に、これらの関数をヘッダファイルに宣言しましょう。`Frustum.h`を開き、次のプログラムを追加してください。

```diff
 using FrustumPtr = std::shared_ptr<Frustum>;

 FrustumPtr CreateFrustum(const VecMath::mat4& matInvProj, float zNear, float zFar);
+bool SphereInsideFrustum(const Collision::Sphere& sphere, const Frustum& frustum);
+bool SphereInsideSubFrustum(const Collision::Sphere& sphere, const SubFrustum& frustum);

 #endif // FRUSTUM_H_INCLUDED
```

これで、球とメインフラスタム、球とサブフラスタムの交差判定は完成です。

### 1.9 LightDataBlockにタイルデータを追加する

球とフラスタムの交差判定を行うことで、ライトがどのタイルに影響するかを調べることができます。あとは、影響するライトをSSBOに記録する方法を考えるだけです。これは次のようなデータ構造によって実現できます。

<p align="center">
<img src="images/tips_07_tiled_shading_data.png" width="66%" /><br>
https://www.zora.uzh.ch/id/eprint/107598/1/a11-olsson.pdf
</p>

この図には3つの配列があります。それぞれの配列の意味は次のとおりです。

| 配列名 | 説明 |
|:------:|:-----|
| `Global Light List`      | 画面に影響するすべてのライトの配列 |
| `Tile Light Index Lists` | タイルに影響するライトインデックス配列を、隙間なく<br>繋いだ配列 |
| `Light Grid`             | `Tile Light Index Lists`内の、各タイルが使用する<br>領域の始点と数 |

フラグメントシェーダでは次の手順でタイルに影響するライトを取得し、明るさを計算します。

>1. 自分(フラグメント)が属するタイルを求める。
>2. `Light Grid`から`Tile Light Index Lists`内の始点とインデックス数を取得。
>3. 始点とインデックス数を利用して、`Global Light List`からライトデータを取得。
>4. forで範囲内のすべてのライトデータについて明かるさを計算。

まず、3つの配列をフラグメントシェーダの`LightDataBlock`に追加します。ライトのインデックスを格納するメンバの名前は`lightIndices`(ライト・インディシーズ)、ライトの数を格納するメンバの名前は`lightCounts`(ライト・カウンツ)とします。

`standard_3D.frag`を開き、`LightDataBlock`の定義を次のように変更してください。

```diff
   vec4 position; // 座標
   vec4 color;    // 色および明るさ
 };
+
+// TBR用タイル情報(CPP側と数値を一致させること)
+const int tileCountX = 32; // 横のタイル数
+const int tileCountY = 18; // 縦のタイル数
+const int lightsPerTile = 256; // 1タイルの想定ライト数
+const int maxLightIndexCount =
+  tileCountX * tileCountY * lightsPerTile; // 最大ライトインデックス数

 // 点光源用SSBO
 layout(std430, binding=1) readonly buffer LightDataBlock
 {
-  int lightCount;
-  int dummy[3];
+  vec2 tileSize; // タイルの大きさ
+  int dummy[2];
+  uint lightIndices[maxLightIndexCount]; // ライトのインデックス
+  uvec2 lightIndexRanges[tileCountY][tileCountX]; // タイルごとのインデックス領域
   Light lightList[];
 };
```

### 1.10 ComputePointLight関数をタイルに対応させる

次に、追加した配列を使ってライトの明るさを計算します。`ComputePointLight`関数を次のように変更してください。

```diff
   const float specularPower = material.specularFactor.x;
   vec3 d = vec3(0);
   vec3 s = vec3(0);
+  // スクリーン座標からライトインデックスの範囲を取得
+  uvec2 tileId = uvec2(gl_FragCoord.xy * tileSize);
+  uvec2 lightRange = lightIndexRanges[tileId.y][tileId.x];
-  for (int i = 0; i < lightCount; ++i) {
+  for (uint i = lightRange.x; i < lightRange.x + lightRange.y; ++i) {
     // フラグメントからライトへ向かうベクトルを計算
+    uint lightIndex = lightIndices[i];
-    vec3 lightVector = lightList[i].position.xyz - inPosition;
+    vec3 lightVector = lightList[lightIndex].position.xyz - inPosition;
     float lengthSq = dot(lightVector, lightVector);
 
     // 面の傾きによる明るさの変化量を計算
     float theta = 1;
     float specularFactor = 1;
     if (lengthSq > 0) {
       // ランベルトの余弦則によって拡散反射の明るさを計算
       vec3 direction = normalize(lightVector);
       theta = max(dot(normal, direction), 0);
 
       // 正規化Blinn-Phong法によって鏡面反射の明るさを計算
       vec3 halfVector = normalize(direction + cameraVector);
       float dotNH = max(dot(normal, halfVector), 0);
       specularFactor = pow(dotNH, specularPower);
     }
 
     // 変化量をかけ合わせて明るさを求め、ライトの明るさ変数に加算
     float intensity = 1.0 / (1.0 + lengthSq); // 距離による明るさの減衰
-    vec3 color = lightList[i].color.rgb * theta * intensity; 
+    vec3 color = lightList[lightIndex].color.rgb * theta * intensity; 
     d += color;
     s += color * specularFactor;
   }
```

変更部分は、ライトのインデックスとしてfor文の添え字を直接使うのではなく、`lightIndices`内の`lightRange`で指定した範囲から取得したところです。

これによって、常にすべてのライトを計算することなく、タイルに割り当てられたライトだけを計算すれば済みます。タイルにすることによるシェーダの変更は、たったこれだけです。

フラグメントのスクリーン座標は`gl_FragCoord`という変数で取得できます。スクリーン座標をタイルサイズで割れば、フラグメントを含むタイルのインデックスが得られます。除算を避けるため、`tileSize`変数には逆数を代入する予定です。

### 1.11 LightBuffer::EndUpdate関数に引数を追加する

次はゲームエンジンをタイルに対応させます。今回の本命はこちら側です。最初に、`Frustum`型の先行宣言を追加します。`LightBuffer.h`を開き、次のプログラムを追加してください。

```diff
 #include <vector>
 #include <memory>
 
 // 先行宣言
+struct Frustum;
+struct SubFrustum;
 class LightBuffer;
 using LightBufferPtr = std::shared_ptr<LightBuffer>;
```

ライトインデックス配列の作成は、SSBOへデータをコピーする直前に行うことにします。SSBOへのコピーは`EndUpdate`メンバ関数で行っているので、作成機能もこの関数に追加しましょう。

ライトインデックス配列の作成にはフラスタムとビュー行列が必要となります。そこで、この2つを`EndUpdate`メンバ関数の引数にします。`EndUpdate`メンバ関数の宣言を次のように変更してください。

```diff
   void BeginUpdate();
   void AddPointLight(
     const VecMath::vec3& position, const VecMath::vec3& color);
-  void EndUpdate();
+  void EndUpdate(const Frustum& frustum,
+    const VecMath::mat4& matView, const VecMath::vec2& screenSize);
   void SwapBuffers();

   // SSBOのバインド
```

次に`LightBuffer.cpp`を開き、`Frustum.h`をインクルードしてください。

```diff
 */
 #include "glad/glad.h"
 #include "LightBuffer.h"
+#include "Frustum.h"
 #include "Debug.h"
 #include <algorithm>
```

続いて、`EndUpdate`メンバ関数の定義を次のように変更してください。

```diff
 /**
 * ライトデータの作成を終了
+*
+* @param frustum    表示範囲を示す視錐台
+* @param matView    ビュー行列
+* @param windowSize 画面の大きさ(ピクセル単位)
 */
-void LightBuffer::EndUpdate()
+void LightBuffer::EndUpdate(const Frustum& frustum
+  const mat4& matView, const vec2& windowize)
 {
   if (isUpdaring) {
```

「ビュー行列」は、ライトの座標をビュー座標系に変換するために使います。「画面の大きさ」はシェーダにコピーする`LightDataBlock`の`tileSize`(タイル・サイズ)を計算するために使います。

引数を変更したので、`EndUpdate`を呼び出しているプログラムも修正しなくてはなりません。`Engine.cpp`を開き、`Frustum.h`をインクルードしてください。

```diff
 #include "GltfMesh.h"
 #include "VertexArray.h"
 #include "LightBuffer.h"
+#include "Frustum.h"
 #include "Component/Camera.h"
 #include "Component/MeshRenderer.h"
```

次に`UpdateGameObject`メンバ変数の定義を次のように変更してください。

```diff
       e->Update(deltaTime);
     }
   } // for list
+
+  // TBR用のフラスタムを作成
+  const auto& camera = GetMainCamera();
+  const float aspect =
+    static_cast<float>(camera->viewport.width) / camera->viewport.height;
+  const mat4 matProj = mat4::Perspective(
+    radians(camera->fovY), aspect, camera->near, camera->far);
+  const auto frustum = CreateFrustum(matProj, camera->near, camera->far);
+
+  // ライトデータをGPUメモリにコピー
+  const mat4 matInvRotation =
+    mat4::RotateZ(-cameraObject->rotation.z) *
+    mat4::RotateX(-cameraObject->rotation.x) *
+    mat4::RotateY(-cameraObject->rotation.y);
+  const mat4 matView =
+    matInvRotation * mat4::Translate(-cameraObject->position);
-  lightBuffer->EndUpdate();
+  lightBuffer->EndUpdate(*frustum, matView, GetWindowSize());
}
```

フラスタムを作成するにはプロジェクション行列が必要となります。プロジェクション行列はパーティクルの描画で使ったのと同じプログラムで作成できます。

さらに、`EndUpdate`メンバ関数にはビュー行列が必要となります。ビュー行列はカメラの回転と平行移動の計算を逆にすることで作成できます。

>「計算を逆にする」というのは「回転と平行移動を負にする」、「行列を掛ける順序を逆にする」の2つによって実現できます。また、`inverse`関数でも作成可能です。

### 1.12 ライトの到達半径を設定する

本来、ライトの光は無限の彼方にまで到達します。しかし、タイルベースドレンダリングではライティングの計算時間を減らすために、意図的に光が到達する半径を制限します。

到達半径を指定できるように、`LightData`構造体と`AddPointLight`メンバ関数に変更を加えます。`LightBuffer.h`を開き、`LightData`構造体の定義を次のように変更してください。

```diff
   // シェーダに送るライトデータ
   struct LightData
   {
-    VecMath::vec4 position; // 座標(wは未使用)
+    VecMath::vec3 position; // 座標
+    float radius;           // 到達半径
     VecMath::vec4 color;    // 色と明るさ(wは未使用)
   };
```

次に、`AddPointLight`メンバ関数の宣言を次のように変更してください。

```diff
   // ライトデータ作成
   void BeginUpdate();
   void AddPointLight(
-    const VecMath::vec3& position, const VecMath::vec3& color);
+    const VecMath::vec3& position, const VecMath::vec3& color, float radius = 0);
   void EndUpdate(const Frustum& frustum,
     const VecMath::mat4& matView, const VecMath::vec2& screenSize);
```

続いて`LightBuffer.cpp`を開き、`AddPointLight`メンバ関数の定義を次のように変更してください。

```diff
 * ライトデータを追加
 *
 * @param position ライトの座標
 * @param color    ライトの色および明るさ
+* @param radius   ライトの到達半径(0=自動計算)
 */
-void LightBuffer::AddPointLight(const vec3& position, const vec3& color,
+void LightBuffer::AddPointLight(const vec3& position, const vec3& color, float radius)
 {
   if (isUpdating) {
+    // ライトの到達半径を計算
+    if (radius <= 0) {
+      const float K = std::max(color.x, std::max(color.y, color.z));
+      constexpr float Lmin = 0.02f;
+      radius = sqrt(K / Lmin - 1);
+    }
     buffer.push_back(
-      LightData{ vec4(position, 0), vec4(color, 0) });
+      LightData{ position, radius, vec4(color, 0) });
   }
```

ライトの明るさを`K`とすると、光源中心からの距離`D`における明るさ`L`は、`L = K / (1 + D^2)`という式(1)で表せます(フラグメントシェーダを参照)。

この式から光の到達距離を求めるには、まず十分に暗いとみなす明るさ`L(min)`を決めます。そして、`L(min)`を`L`に代入することで`D`について式を解きます。

&emsp;L(min) = K / (1 + D^2)&emsp;&emsp;&emsp;...(1)<br>
&emsp;L(min) * (1 + D^2) = K<br>
&emsp;L(min) + L(min) * D^2 = K<br>
&emsp;L(min) * D^2 = K - L(min)<br>
&emsp;D^2 = (K - L(min)) / L(min)<br>
&emsp;D^2 = K / L(min) - 1<br>
&emsp;D = √(K / L(min) - 1)&emsp;&emsp;&emsp;...(2)<br>

式(2)によって、明るさが`L(min)`になる距離`D`を求めることができます。この`D`がライトの到達半径になります。

次に`Engine.h`を開き、`Engine`クラス定義にある`AddPointLight`メンバ関数の宣言を次のように変更してください。

```diff
   void SetBloomStrength(float s) { bloomStrength = s; }

   // ライトの操作
   void AddPointLightData(const VecMath::vec3& position,
-    const VecMath::vec3& color);
+    const VecMath::vec3& color, float radius = 0);

   // パーティクルエミッタの操作
   ParticleEmitterPtr AddParticleEmitter(
```

続いて`Engine.cpp`を開き、`AddPointLight`メンバ関数の定義を次のように変更してください。

```diff
 * ポイントライトのデータをライトバッファに追加する
 *
 * @param position ライトの座標
 * @param color    ライトの色および明るさ
+* @param radius   ライトの到達半径(0=自動計算)
 */
-void Engine::AddPointLightData(const vec3& position, const vec3& color)
+void Engine::AddPointLightData(const vec3& position, const vec3& color, float radius)
 {
-  lightBuffer->AddPointLight(position, color);
+  lightBuffer->AddPointLight(position, color, radius);
 }

 /**
```

それから、`Light`コンポーネントにも影響半径を追加します。プロジェクトの`Src/Component`フォルダにある`Light.h`を開き、次のプログラムを追加してください。

```diff
   Type type = Type::PointLight; // ライトの種類
   VecMath::vec3 color = { 1, 1, 1 }; // ライトの色
   float intensity = 1; // ライトの明るさ
+  float radius = 0; // 影響半径(0=自動計算)
 };

 #endif // COMPONENT_LIGHT_H_INCLUDED
```

続いて`Light.cpp`を開き、`Update`メンバ関数の定義に次のプログラムを追加してください。

```diff
   Engine* engine = gameObject.engine;

   // ライトデータを追加
-  engine->AddPointLightData(gameObject.position, color * intensity);
+  engine->AddPointLightData(gameObject.position, color * intensity, radius);
 }
```

これで、意図的に影響半径を小さくしたり、大きくすることが可能になりました。例えば、マップのシンボル的な光源は大きく、火花のようにすぐ消えるエフェクトは小さくすることで、描画効率が向上します。

### 1.13 LightDataBlock構造体を定義する

次にライトインデックス配列を作成します。それなりのコード量になるので、3つのプライベートメンバ関数に分けて定義することにします。`LightBuffer.h`を開き、`LightBuffer`クラスのプライベートメンバに次のプログラムを追加してください。

```diff
   void Bind(GLuint index);
   void Unbind(GLuint index);

 private:
+  // タイルごとのライトインデックス配列の構築
+  struct Range;
+  struct LightDataBlock;
+
+  void RemoveIneffectiveLight(const Frustum& frustum,
+    const VecMath::mat4& matView,
+    std::vector<VecMath::vec3>& posView);
+
+  void BuildLightDataBlock(const Frustum& frustum,
+    const std::vector<VecMath::vec3>& posView,
+    LightDataBlock& lightDataBlock);
+
+  void BuildLightIndices(const SubFrustum& subFrustum,
+    const std::vector<VecMath::vec3>& posView,
+    uint32_t* lightIndices, uint32_t& lightIndexCount);
+
   // シェーダに送るライトデータ
   struct LightData
```

`Range`(レンジ)構造体は、シェーダ側の`LightDataBlock`にある`lightIndexRanges`配列の`uvec2`型に相当します。実装詳細を公開する必要はないので、メンバ変数の引数にできるように宣言だけを行っています。

なお、シェーダ側に`Range`型を定義していない理由は、シェーダでは`uint`のような1要素の型を複数使うより、`uvec2`のような2要素以上をまとめたベクトル型のほうが効率に処理されるからです。

>**【スカラーとベクトル】**<br>
>`float`や`int`のように、1要素で構成される型のことを「スカラー型」、`vec2`, `vec3`, `vec4`のように2つ以上の要素で構成される型のことを「ベクトル型」といいます。

CPU側ではスカラー型でもベクトル型でも処理能力に違いはないので、コードを読みやすくするために構造体を定義しているわけです。

`struct LightDataBlock`はシェーダ側の`LightDataBlock`に対応する構造体です。この型も宣言だけを行っています。

`RemoveIneffectiveLight`(リムーブ・インエフェクティブ・ライト、「効果のないライトを除外する」という意味)メンバ関数は、視錐台の範囲に影響しないライトを除去します。

`BuildLightDataBlock`(ビルド・ライト・データ・ブロック、「ライトデータブロックを構築する」という意味)メンバ関数は、すべてのタイルのライトインデックス配列の範囲を設定します。

`BuildLightIndices`(ビルド・ライト・インディシーズ、「ライトインデックス配列を構築する」という意味)メンバ関数は、タイルごとのライトインデックス配列を作成します。

次に`LightDataBlock`構造体を定義します。`LightBuffer.cpp`を開き、`VecMath`を`using`宣言するプログラムの下に、次のプログラムを追加してください。

```diff
 #include <algorithm>

 using namespace VecMath;
+
+// TBR用タイル情報(シェーダ側と一致させること)
+
+// 最大ライトインデックス数
+constexpr int maxLightIndexCount =
+  Frustum::tileCountX * Frustum::tileCountY * Frustum::lightsPerTile;
+
+// タイルのライトインデックス配列の範囲
+struct LightBuffer::Range
+{
+  uint32_t begin = 0; // ライトインデックス配列の開始位置
+  uint32_t count = 0; // ライトインデックスの数
+};
+
+// SSBOに設定するライトデータ
+struct LightBuffer::LightDataBlock
+{
+  vec2 tileSize; // タイルの大きさ
+  float dummy[2];// 16バイトアラインのためのダミー領域
+  uint32_t lightIndices[maxLightIndexCount]; // ライトインデックス配列
+
+  // タイルごとのライトインデックス配列の範囲
+  Range lightIndexRanges[Frustum::tileCountY][Frustum::tileCountX];
+};

 /**
 * コンストラクタ
```

上記のプログラムのように、クラス内で宣言した構造体やクラスは、`::`演算子を使うことで宣言と定義を別々に行うことができます。これによって、宣言だけを公開し、実装は非公開にすることができます。

### 1.14 RemoveIneffectiveLight関数を定義する

ライトインデックス配列の作成は以下の手順で行います。

1. `RemoveIneffectiveLight`: メインフラスタムと交差しているライトだけを`buffers`配列に残す。
2. `BuildLightDataBlock`: すべてのサブフラスタムについて`BuildLightIndices`関数を呼び出し、タイルが使用するライトインデックス配列の範囲を設定する。
3. `BuildLightIndices`: 全てのライトとの交差判定を行い、ライトインデックス配列を更新する。

それでは`RemoveIneffectiveLight`メンバ関数から定義していきましょう。このメンバ関数は、「ライトがメインフラスタムと交差しているかどうか」、言い換えると「画面に影響するライトかどうか」を調べます。

この処理によって、明らかに無関係なライトを除外し、GPUに転送する必要のあるライトデータを削減できます。`AddPointLight`メンバ関数の定義の下に、次のプログラムを追加してください。

```diff
       LightData{ position, radius, vec4(color, 0) });
   }
 }
+
+/**
+* buffer配列から視錐台に影響しないライトデータを削除する
+*
+* @param[in]  frustum 視錐台
+* @param[in]  matView ビュー行列
+* @param[out] posView ビュー座標系に変換したライト座標の配列
+*/
+void LightBuffer::RemoveIneffectiveLight(const Frustum& frustum,
+  const mat4& matView, std::vector<vec3>& posView)
+{
+  std::vector<LightData> lightInFrustum;
+  lightInFrustum.reserve(buffer.size());
+  posView.reserve(buffer.size());
+
+  // メインフラスタムと交差しているライトだけをピックアップ
+  for (uint32_t i = 0; i < buffer.size(); ++i) {
+    // ライトの座標をビュー座標系に変換
+    const LightData& e = buffer[i];
+    const vec3 pos = vec3(matView * vec4(e.position, 1));
+
+    // ライトの影響範囲とメインフラスタムの交差判定を行う
+    const Collision::Sphere s = { pos, e.radius };
+    if (SphereInsideFrustum(s, frustum)) {
+      // 交差しているのでライトを登録
+      lightInFrustum.push_back(e);
+      posView.push_back(pos);
+    }
+  }
+
+  // ライトデータを入れ替える
+  buffer.swap(lightInFrustum);
+}

 /**
 * ライトデータの作成を終了
```

このプログラムでは`SphereInsideFrustum`関数によって、ライトの影響半径とフラスタムの交差判定を行い、交差しているライトだけを`lightInFrustum`(ライト・イン・フラスタム)配列に追加します。

そして、関数の最後で`buffer`と`lightInFrustum`を入れ替えることで、`buffer`から「フラスタムに影響しないライト」を除去しています。

ビュー行列はライトの座標を変換するために使います。フラスタムはビュー空間で定義されているので、ライトの座標もビュー座標系でなくては正しい判定が行えないからです。

また、ビュー座標系のライト座標は次に作成する`BuildLightDataBlock`メンバ関数でも必要なので、引数で受け取ったオブジェクトに格納しています。

### 1.15 BuildLightDataBlock関数を定義する

続いて、`BuildLightDataBlock`メンバ関数を定義します。`RemoveIneffectiveLight`メンバ関数の定義の下に、次のプログラムを追加してください。

```diff
   // ライトデータを入れ替える
   buffer.swap(lightInFrustum);
 }
+
+/**
+* ライトインデックス配列を構築する
+*
+* @param[in]  frustum 視錐台
+* @param[in]  posView ビュー座標系に変換したライト座標の配列
+* @param[out] lightDataBlock ライトインデックス配列を格納するオブジェクト
+*/
+void LightBuffer::BuildLightDataBlock(const Frustum& frustum,
+  const std::vector<VecMath::vec3>& posView,
+  LightDataBlock& lightDataBlock)
+{
+  // すべてのタイル(サブフラスタム)についてループ
+  uint32_t lightIndexCount = 0; // ライトインデックス数
+  for (int y = 0; y < Frustum::tileCountY; ++y) {
+    for (int x = 0; x < Frustum::tileCountX; ++x) {
+      // ライトインデックス配列の位置を範囲の先頭として設定
+      Range& range = lightDataBlock.lightIndexRanges[y][x];
+      range.begin = lightIndexCount;
+
+      // サブフラスタムと交差するライトをインデックス配列に登録
+      BuildLightIndices(frustum.sub[y][x],
+        posView, lightDataBlock.lightIndices, lightIndexCount);
+
+      // ライトインデックスの数を設定
+      range.count = lightIndexCount - range.begin;
+
+      // 容量不足なら構築を終了する
+      if (lightIndexCount >= maxLightIndexCount) {
+        return; // 容量不足
+      }
+    } // for x
+  } // for y
+}

 /**
 * ライトデータの作成を終了
```

`Range`構造体に設定している値に注意してください、`lightIndexCount`変数は「現在のライトインデックス配列の要素数」を表します。

`BuildLightIndices`関数を実行すると、タイルに影響するライトのインデックス数だけ`lightIndexCount`が増加します。つまり、関数実行前後の`lightIndexCount`の差が、タイルのライトインデックス数になるわけです。

また、このプログラムはすべてのタイルを処理するために2重ループになっています。フラスタムを細かく分割し、タイル数を増やすほどシェーダの効率は向上しますが、分割しすぎるとこの部分のループ回数が増え、CPU側の効率が低下します。

そのため、分割数は多すぎず少なすぎずというバランスが大切になります。

### 1.16 BuildLightIndices関数を定義する

次に`BuildLightIndices`メンバ関数を定義します。この関数は、あるタイルのサブフラスタムと全てのライトの交差判定を行い、タイルに影響するライトのインデックスをライトインデックス配列に記録します。

`BuildLightDataBlock`メンバ関数の定義の下に、次のプログラムを追加してください。

```diff
     } // for x
   } // for y
 }
+
+/**
+* サブフラスタムと交差するライトをインデックス配列に登録
+*
+* @param[in]      subFrustum サブ視錐台
+* @param[in]      posView ビュー座標系に変換したライト座標の配列
+* @param[in, out] lightIndices ライトインデックス配列
+* @param[in, out] lightIndexCount ライトインデックス数
+*/
+void LightBuffer::BuildLightIndices(const SubFrustum& subFrustum,
+  const std::vector<VecMath::vec3>& posView,
+  uint32_t* lightIndices, uint32_t& lightIndexCount)
+{
+  // すべてのライトについてループ
+  for (uint32_t i = 0; i < buffer.size(); ++i) {
+    // ライトの影響範囲とサブフラスタムの交差判定を行う
+    const Collision::Sphere s = { posView[i], buffer[i].radius };
+    if (SphereInsideSubFrustum(s, subFrustum)) {
+      // ライトインデックス配列にインデックスを記録
+      lightIndices[lightIndexCount] = i;
+      ++lightIndexCount;
+
+      // 容量不足なら構築を終了する
+      if (lightIndexCount >= maxLightIndexCount) {
+        LOG_WARNING("ライトインデックス配列が不足しています(サイズ=%d)",
+          maxLightIndexCount);
+        break;
+      }
+    }
+  } // for i
+}

 /**
 * ライトデータの作成を終了
```

`BuildLightIndices`メンバ関数は、全てのライトとサブフラスタムの交差判定を行い、交差したライトのインデックスをライトインデックス配列に追加します。

### 1.17 EndUpdate関数を修正する

シェーダの`LightDataBlock`の構造を変更したので、`EndUpdate`メンバ関数のデータコピープログラムにも修正が必要です。ここで、作成したプライベートメンバ関数を呼び出します。

`EndUpdate`メンバ関数の定義を次のように変更してください。

```diff
   if (isUpdaring) {
     isUpdaring = false;
+ 
+    // 視錐台に影響しないライトデータを削除
+    std::vector<vec3> posView; // ビュー座標系に変換したライト座標の配列
+    RemoveIneffectiveLight(frustum, matView, posView);
+
+    // ライトをタイルに配置
+    auto lightDataBlock = std::make_shared<LightDataBlock>();
+    BuildLightDataBlock(frustum, posView, *lightDataBlock);
+
+    // タイルサイズを設定
+    lightDataBlock->tileSize =
+      vec2(Frustum::tileCountX, Frustum::tileCountY) / windowSize;

     // データをGPUメモリにコピー
     ssbo->WaitSync();
     uint8_t* p = ssbo->GetMappedAddress();
```

`tileSize`(タイル・サイズ)は、フラグメントの座標をタイルのインデックスに変換するために使います。タイルのインデックスは次の式で求められます。

&emsp;タイルインデックス = フラグメントの座標 / ウィンドウサイズ * タイル数

この式の後半部分を次のように変形します。

&emsp;タイルインデックス
&emsp;&emsp; = フラグメントの座標 * (1 / ウィンドウサイズ) * タイル数<br>
&emsp;&emsp; = フラグメントの座標 * (タイル数 / ウィンドウサイズ)<br>

`(タイル数 / ウィンドウサイズ)`をGPUメモリの`tileSize`変数にコピーし、フラグメントシェーダでは`tileSize`を掛けてタイルインデックスを計算します。

次に、データをGPUメモリにコピーするプログラムを修正して、`LightDataBlock`をコピーするようにします。データをGPUメモリにコピーするプログラムを、次のように変更してください。

```diff
     // データをGPUメモリにコピー
     ssbo->WaitSync();
     uint8_t* p = ssbo->GetMappedAddress();
-    const int lightCount[4] = { static_csat<int>(buffer.size()) }
-    memcpy(p, buffer.data(), sizeof(lightCount));
+    memcpy(p, lightDataBlock.get(), sizeof(LightDataBlock));

     if ( ! buffer.empty()) {
-      p += sizeof(lightCount);
+      p += sizeof(LightDataBlock);
       const size_t size = std::min<size_t>(
-        ssbo->GetSize(), buffer.size() * sizeof(LightData));
+        ssbo->GetSize() * sizeof(lightDataBlock),
+        buffer.size() * sizeof(LightData));
       memcpy(p, buffer.data(), size);
 
       // バッファの容量を最小化
```

これで、タイルに影響するライトの情報がSSBOにコピーされるようになりました。

プログラムが書けたらビルドして実行してください。見た目の変化はありませんが、処理速度は向上しているはずです。

### 1.18 ライトの範囲を制限する

カメラを動かしてみると分かりますが、前節で「見た目の変化はない」と書いたのは大嘘です。実際には鏡面反射に大きな影響が出ています。カメラの向きや位置によって、ライトの鏡面反射が描画されたりされなかったりするはずです。

<p align="center">
<img src="images/tips_07_result_1.jpg" width="45%" /><br>
[赤枠内の鏡面反射が消えている]
</p>

鏡面反射は入射光の大半を同じ方向に反射するため、かなり遠くの光源からの光でも強い反射を起こすからです。

このため、距離が離れているオブジェクトが偶然サブフラスタム内に存在すると、鏡面反射が発生してライトが描画されます。そして、サブフラスタムから外れるとライトは描画されなくなります。

つまり、現在のライトの影響半径の計算式は、実際には拡散反射にしか有効ではないのです。かといって、鏡面反射に対応できるように影響半径を広げると、事実上タイルベースドレンダリングが無意味になってしまいます。

そこで、影響範囲の境界において鏡面反射の明るさが0になるように、反射光の明るさを徐々に減衰させることにします。これは物理的には完全な嘘っぱちですが、現代のほぼすべてのゲームエンジンで使われている手法です。

`standard_3D.frag`を開き、ComputePointLight`関数の定義に次のプログラムを追加してください。

```diff
       vec3 halfVector = normalize(direction + cameraVector);
       float dotNH = max(dot(normal, halfVector), 0);
       specularFactor = pow(dotNH, specularPower);
+
+      // 影響半径境界に近づくにつれて、光の影響が0になるようにする
+      const float radius = lightList[lightIndex].position.w;
+      const float smoothFactor = clamp(1 - pow(sqrt(lengthSq) / radius, 4), 0, 1);
+      theta *= smoothFactor * smoothFactor;
     }

     // 変化量をかけ合わせて明るさを求め、ライトの明るさ変数に加算
```

このプログラムの計算式は`Unreal Engine 4`で用いられているものです。物理的な根拠はありませんが、できるだけ不自然にならないように徐々に明るさを減衰しつつ、かつ比較的計算が単純であることから選ばれたようです。

>式については以下のURLを参照:<br>
>`https://blog.selfshadow.com/publications/s2013-shading-course/karis/s2013_pbs_epic_notes_v2.pdf`

プログラムが書けたらビルドして実行してください。カメラを動かしたとき、鏡面反射が出たり消えたりしなくなっていれば成功です。

<p align="center">
<img src="images/tips_07_result_2.jpg" width="45%" /><br>
[全体的に鏡面反射が弱くなっている]
</p>

### 1.19 タイルごとのライト数を確認する

処理速度が向上したと言われても、見た目ではあまり違いが分からないかもしれません。そこで、シェーダに手を加えて「タイルごとのライト数を視覚化」してみましょう。

`standard_3D.frag`を開き、次のプログラムを追加してください。

```diff
   ambient += specularAmbient * specularRatio;

   outColor.rgb = (diffuse + specular) * shadow + ambient;
+
+#if 1
+  // jetカラーマップによってライト数を表示
+  uvec2 tileId = uvec2(gl_FragCoord.xy * tileSize);
+  uvec2 range = lightIndexRanges[tileId.y][tileId.x];
+  float level = min(float(range.y) / 100, 1);
+  vec3 jet = vec3(
+    level < 0.7 ? 4 * level - 1.5 : 4.5 - level * 4,
+    level < 0.5 ? 4 * level - 0.5 : 3.5 - level * 4,
+    level < 0.3 ? 4 * level + 0.5 : 2.5 - level * 4);
+  outColor.rgb = mix(outColor.rgb, jet, 0.5);
+#endif
 }
```

「jet(ジェット)カラーマップ」は画像化された情報を視覚化する手法の一つです。jetカラーマップによって、0～1の値を色のグラデーションとして表示できます。

>上記のコードは以下のURLにある`glsl/MATLAB_jet.frag`を参照しました<br>
>`https://github.com/kbinani/colormap-shaders`

プログラムが書けたらビルドして実行してください。タイルごとに青、緑、黄、赤のグラデーションが表示されていたら成功です。

ライト数が少ないタイルは青で表示され、ライト数が増えるにつれて水色、緑、黄色、赤、暗赤色と変化します。暗赤色のタイルは100個前後のライトが影響していることになります。

<p align="center">
<img src="images/tips_07_result_3.jpg" width="45%" />
</p>

<pre class="tnmai_assignment">
<strong>【課題01】</strong>
<code>MainGameScene.cpp</code>で作成している<code>Light</code>コンポーネントの<code>intensity</code>メンバ変数の値を3や10に変更して、カラーマップがどのように変化するかを確認しなさい。
</pre>

<pre class="tnmai_assignment">
<strong>【課題02】</strong>
インテンシティを5に戻し、<code>Light</code>コンポーネントの<code>radius</code>メンバ変数に10や100といった値を指定して、カラーマップがどのように変化するかを確認しなさい。
</pre>

<pre class="tnmai_assignment">
<strong>【課題03】</strong>
インテンシティと影響半径を元に戻し、jetカラーマップを作成するプログラムの<code>#if 1</code>の部分を<code>#if 0</code>にして、jetカラーマップを非表示にしなさい。
</pre>

>**【まとめ】**<br>
>
>* 画面を小さなタイル状の領域に分割して描画することを「タイル・ベースド・レンダリング(TBR)」という。
>* タイルは3D空間では錘台形(フラスタム)になる。
>* 個々のライトが目に見える影響を及ぼす半径には限界があるため、タイル単位でライトを選択することでライティングの計算量を減らすことができる。
>* 分割数が少なすぎるとタイルのライト数が増加するためTBRの恩恵が少なくなる。逆に多すぎるとタイルの計算に時間がかかり、やはりTBRの恩恵が少なくなる。TBRを最大限に活かすには、適切な分割数を選択する必要がある。
>* 各タイルの状態を視覚的に調べるには「カラーマップ」を使う。なお、このような強度を視覚的に表す手法は「ヒート・マップ」と呼ばれる。

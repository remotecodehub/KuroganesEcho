<?xml version="1.0" encoding="UTF-8"?>
<tileset version="1.10" tiledversion="1.11.0" name="Skeleton_Archer" tilewidth="128" tileheight="128" tilecount="70" columns="0" objectalignment="bottom">
  <grid orientation="orthogonal" width="128" height="128"/>
    <tile id="0">
      <properties>
        <property name="Character" value="Skeleton_Archer"/>
        <property name="State" value="Attack_1"/>
        <property name="RenderScale" type="float" value="0.75"/>
        <property name="Loop" type="bool" value="true"/>
      </properties>
      <animation>
        <frame tileid="0" duration="90"/>
        <frame tileid="1" duration="90"/>
        <frame tileid="2" duration="90"/>
        <frame tileid="3" duration="90"/>
        <frame tileid="4" duration="90"/>
      </animation>
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Attack_1_00.png"/>
    </tile>
    <tile id="1">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Attack_1_01.png"/>
    </tile>
    <tile id="2">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Attack_1_02.png"/>
    </tile>
    <tile id="3">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Attack_1_03.png"/>
    </tile>
    <tile id="4">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Attack_1_04.png"/>
    </tile>
    <tile id="5">
      <properties>
        <property name="Character" value="Skeleton_Archer"/>
        <property name="State" value="Attack_2"/>
        <property name="RenderScale" type="float" value="0.75"/>
        <property name="Loop" type="bool" value="true"/>
      </properties>
      <animation>
        <frame tileid="5" duration="90"/>
        <frame tileid="6" duration="90"/>
        <frame tileid="7" duration="90"/>
        <frame tileid="8" duration="90"/>
      </animation>
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Attack_2_00.png"/>
    </tile>
    <tile id="6">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Attack_2_01.png"/>
    </tile>
    <tile id="7">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Attack_2_02.png"/>
    </tile>
    <tile id="8">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Attack_2_03.png"/>
    </tile>
    <tile id="9">
      <properties>
        <property name="Character" value="Skeleton_Archer"/>
        <property name="State" value="Attack_3"/>
        <property name="RenderScale" type="float" value="0.75"/>
        <property name="Loop" type="bool" value="true"/>
      </properties>
      <animation>
        <frame tileid="9" duration="90"/>
        <frame tileid="10" duration="90"/>
        <frame tileid="11" duration="90"/>
      </animation>
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Attack_3_00.png"/>
    </tile>
    <tile id="10">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Attack_3_01.png"/>
    </tile>
    <tile id="11">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Attack_3_02.png"/>
    </tile>
    <tile id="12">
      <properties>
        <property name="Character" value="Skeleton_Archer"/>
        <property name="State" value="Dead"/>
        <property name="RenderScale" type="float" value="0.75"/>
        <property name="Loop" type="bool" value="false"/>
      </properties>
      <animation>
        <frame tileid="12" duration="140"/>
        <frame tileid="13" duration="140"/>
        <frame tileid="14" duration="140"/>
        <frame tileid="15" duration="140"/>
        <frame tileid="16" duration="140"/>
      </animation>
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Dead_00.png"/>
    </tile>
    <tile id="13">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Dead_01.png"/>
    </tile>
    <tile id="14">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Dead_02.png"/>
    </tile>
    <tile id="15">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Dead_03.png"/>
    </tile>
    <tile id="16">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Dead_04.png"/>
    </tile>
    <tile id="17">
      <properties>
        <property name="Character" value="Skeleton_Archer"/>
        <property name="State" value="Evasion"/>
        <property name="RenderScale" type="float" value="0.75"/>
        <property name="Loop" type="bool" value="true"/>
      </properties>
      <animation>
        <frame tileid="17" duration="100"/>
        <frame tileid="18" duration="100"/>
        <frame tileid="19" duration="100"/>
        <frame tileid="20" duration="100"/>
        <frame tileid="21" duration="100"/>
        <frame tileid="22" duration="100"/>
      </animation>
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Evasion_00.png"/>
    </tile>
    <tile id="18">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Evasion_01.png"/>
    </tile>
    <tile id="19">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Evasion_02.png"/>
    </tile>
    <tile id="20">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Evasion_03.png"/>
    </tile>
    <tile id="21">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Evasion_04.png"/>
    </tile>
    <tile id="22">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Evasion_05.png"/>
    </tile>
    <tile id="23">
      <properties>
        <property name="Character" value="Skeleton_Archer"/>
        <property name="State" value="Hurt"/>
        <property name="RenderScale" type="float" value="0.75"/>
        <property name="Loop" type="bool" value="false"/>
      </properties>
      <animation>
        <frame tileid="23" duration="100"/>
        <frame tileid="24" duration="100"/>
      </animation>
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Hurt_00.png"/>
    </tile>
    <tile id="24">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Hurt_01.png"/>
    </tile>
    <tile id="25">
      <properties>
        <property name="Character" value="Skeleton_Archer"/>
        <property name="State" value="Idle"/>
        <property name="RenderScale" type="float" value="0.75"/>
        <property name="Loop" type="bool" value="true"/>
      </properties>
      <animation>
        <frame tileid="25" duration="120"/>
        <frame tileid="26" duration="120"/>
        <frame tileid="27" duration="120"/>
        <frame tileid="28" duration="120"/>
        <frame tileid="29" duration="120"/>
        <frame tileid="30" duration="120"/>
        <frame tileid="31" duration="120"/>
      </animation>
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Idle_00.png"/>
    </tile>
    <tile id="26">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Idle_01.png"/>
    </tile>
    <tile id="27">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Idle_02.png"/>
    </tile>
    <tile id="28">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Idle_03.png"/>
    </tile>
    <tile id="29">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Idle_04.png"/>
    </tile>
    <tile id="30">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Idle_05.png"/>
    </tile>
    <tile id="31">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Idle_06.png"/>
    </tile>
    <tile id="32">
      <properties>
        <property name="Character" value="Skeleton_Archer"/>
        <property name="State" value="Shot_1"/>
        <property name="RenderScale" type="float" value="0.75"/>
        <property name="Loop" type="bool" value="true"/>
      </properties>
      <animation>
        <frame tileid="32" duration="100"/>
        <frame tileid="33" duration="100"/>
        <frame tileid="34" duration="100"/>
        <frame tileid="35" duration="100"/>
        <frame tileid="36" duration="100"/>
        <frame tileid="37" duration="100"/>
        <frame tileid="38" duration="100"/>
        <frame tileid="39" duration="100"/>
        <frame tileid="40" duration="100"/>
        <frame tileid="41" duration="100"/>
        <frame tileid="42" duration="100"/>
        <frame tileid="43" duration="100"/>
        <frame tileid="44" duration="100"/>
        <frame tileid="45" duration="100"/>
        <frame tileid="46" duration="100"/>
      </animation>
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_00.png"/>
    </tile>
    <tile id="33">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_01.png"/>
    </tile>
    <tile id="34">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_02.png"/>
    </tile>
    <tile id="35">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_03.png"/>
    </tile>
    <tile id="36">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_04.png"/>
    </tile>
    <tile id="37">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_05.png"/>
    </tile>
    <tile id="38">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_06.png"/>
    </tile>
    <tile id="39">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_07.png"/>
    </tile>
    <tile id="40">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_08.png"/>
    </tile>
    <tile id="41">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_09.png"/>
    </tile>
    <tile id="42">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_10.png"/>
    </tile>
    <tile id="43">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_11.png"/>
    </tile>
    <tile id="44">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_12.png"/>
    </tile>
    <tile id="45">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_13.png"/>
    </tile>
    <tile id="46">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_1_14.png"/>
    </tile>
    <tile id="47">
      <properties>
        <property name="Character" value="Skeleton_Archer"/>
        <property name="State" value="Shot_2"/>
        <property name="RenderScale" type="float" value="0.75"/>
        <property name="Loop" type="bool" value="true"/>
      </properties>
      <animation>
        <frame tileid="47" duration="100"/>
        <frame tileid="48" duration="100"/>
        <frame tileid="49" duration="100"/>
        <frame tileid="50" duration="100"/>
        <frame tileid="51" duration="100"/>
        <frame tileid="52" duration="100"/>
        <frame tileid="53" duration="100"/>
        <frame tileid="54" duration="100"/>
        <frame tileid="55" duration="100"/>
        <frame tileid="56" duration="100"/>
        <frame tileid="57" duration="100"/>
        <frame tileid="58" duration="100"/>
        <frame tileid="59" duration="100"/>
        <frame tileid="60" duration="100"/>
        <frame tileid="61" duration="100"/>
      </animation>
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_00.png"/>
    </tile>
    <tile id="48">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_01.png"/>
    </tile>
    <tile id="49">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_02.png"/>
    </tile>
    <tile id="50">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_03.png"/>
    </tile>
    <tile id="51">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_04.png"/>
    </tile>
    <tile id="52">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_05.png"/>
    </tile>
    <tile id="53">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_06.png"/>
    </tile>
    <tile id="54">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_07.png"/>
    </tile>
    <tile id="55">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_08.png"/>
    </tile>
    <tile id="56">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_09.png"/>
    </tile>
    <tile id="57">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_10.png"/>
    </tile>
    <tile id="58">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_11.png"/>
    </tile>
    <tile id="59">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_12.png"/>
    </tile>
    <tile id="60">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_13.png"/>
    </tile>
    <tile id="61">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Shot_2_14.png"/>
    </tile>
    <tile id="62">
      <properties>
        <property name="Character" value="Skeleton_Archer"/>
        <property name="State" value="Walk"/>
        <property name="RenderScale" type="float" value="0.75"/>
        <property name="Loop" type="bool" value="true"/>
      </properties>
      <animation>
        <frame tileid="62" duration="100"/>
        <frame tileid="63" duration="100"/>
        <frame tileid="64" duration="100"/>
        <frame tileid="65" duration="100"/>
        <frame tileid="66" duration="100"/>
        <frame tileid="67" duration="100"/>
        <frame tileid="68" duration="100"/>
        <frame tileid="69" duration="100"/>
      </animation>
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Walk_00.png"/>
    </tile>
    <tile id="63">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Walk_01.png"/>
    </tile>
    <tile id="64">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Walk_02.png"/>
    </tile>
    <tile id="65">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Walk_03.png"/>
    </tile>
    <tile id="66">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Walk_04.png"/>
    </tile>
    <tile id="67">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Walk_05.png"/>
    </tile>
    <tile id="68">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Walk_06.png"/>
    </tile>
    <tile id="69">
      <image width="128" height="128" source="../../Enemies/GeneratedFrames/Skeleton_Archer/Walk_07.png"/>
    </tile>
</tileset>

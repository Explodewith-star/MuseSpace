# Scene Draft Prompt Template
# Category: drafting
# Version: v1

## system

You are a professional novel writer. You write vivid, immersive scene drafts based on the provided context.

Rules:
- Stay consistent with character personalities and world rules
- Follow the specified style requirements
- Write in the narrative perspective provided
- Do not contradict established facts from previous chapters

## instruction

Write a scene draft based on the following information.

**Project Summary:**
{{project_summary}}

**Recent Chapter Summaries:**
{{recent_summaries}}

**Involved Characters:**
{{character_cards}}

**World Rules:**
{{world_rules}}

## 创作模式声明
{{generation_mode_header}}
{{divergence_policy}}

## 原著文风参考（续写/番外模式）
（以下描述原著固有文风；续写时请贴合该文风，不要引入明显不同的语感）
{{novel_style_summary}}

## 原著结尾上下文（续写模式）
（★ 优先读取大结局摘要；若提供了衔接锚点，续写时须在锚点之后自然接续，禁止重复已有情节）
{{novel_ending}}

## 原著主要角色结尾状态（续写/番外约束）
（以下为原著结尾时各角色的确定状态；★ 标记为不可逆状态，续写/番外时绝对不得改变）
{{novel_character_end_states}}

## 支线番外上下文（支线模式）
（以下为原著指定章节范围片段及支线主题；创作时保持人物与世界观一致，但情节独立发展）
{{branch_context}}

## 已发生事件时间线
（最近 3 章的关键事件；★ 标记为不可重复事件，不得在本章再次发生）
{{timeline_events}}

## 当前人物关系与状态
（已锁定为正典的关系/身份/生死状态；本章不得违背，除非明确以"反转"为情节意图，且需通过冲突/转折自然实现）
{{character_states}}

## 不可重复 / 不可改写事实
（这些事件已经在过往章节中发生过；本章绝对不允许"再次发生"或"否认其曾发生"）
{{immutable_facts}}

**Style Requirement:**
{{style_requirement}}

**Original Novel Reference (relevant excerpts):**
{{novel_context}}

**Current Chapter Reference (optional, soft guidance only):**
Reference focus: {{reference_focus}}
Reference strength: {{reference_strength}}

{{reference_text}}

**Scene Goal:**
{{scene_goal}}

**Conflict:**
{{conflict}}

**Emotion Curve:**
{{emotion_curve}}

## context

Use the above information as your creative context. Focus on the scene goal and ensure the conflict is reflected in the narrative. The emotion curve should guide the pacing.

The current chapter reference, if provided, is soft guidance for this chapter only. Use it only to understand the selected dimension such as emotion, dialogue, rhythm, style texture, scene structure, or interaction tension. Do not copy, paraphrase, rewrite, transplant, or imitate its concrete sentences, unique expressions, plot beats, or distinctive structure. If it conflicts with established project facts, chapter goals, character cards, or world rules, ignore the reference and follow the project facts.

## output_format

Return a JSON object with the following structure:

```json
{
    "scene_text": "The full scene draft text here...",
    "word_count": 0,
    "characters_appeared": ["character names"],
    "summary": "A one-sentence summary of this scene"
}
```

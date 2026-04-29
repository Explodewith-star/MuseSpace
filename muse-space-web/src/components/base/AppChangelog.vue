<script setup lang="ts">
import { computed } from 'vue'
import type { Changelog } from '@/config/changelog'

const props = defineProps<{
  modelValue: boolean
  changelog: Changelog
}>()

const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

function confirm() {
  emit('update:modelValue', false)
}

const typeConfig = {
  new: { icon: 'i-lucide-sparkles', label: '新功能', cls: 'type-new' },
  fix: { icon: 'i-lucide-wrench', label: '修复', cls: 'type-fix' },
  tip: { icon: 'i-lucide-lightbulb', label: '建议', cls: 'type-tip' },
  change: { icon: 'i-lucide-refresh-cw', label: '变更', cls: 'type-change' },
} as const

const groupedItems = computed(() => {
  // 按 type 排序：new → change → fix → tip
  const order = ['new', 'change', 'fix', 'tip']
  return [...props.changelog.items].sort(
    (a, b) => order.indexOf(a.type) - order.indexOf(b.type),
  )
})
</script>

<template>
  <Teleport to="body">
    <Transition name="cl-fade">
      <div v-if="modelValue" class="cl-overlay" @click.self="confirm">
        <Transition name="cl-slide">
          <div v-if="modelValue" class="cl-dialog" role="dialog" aria-modal="true">
            <!-- 头部 -->
            <div class="cl-header">
              <div class="cl-header__icon">
                <i class="i-lucide-megaphone" />
              </div>
              <div>
                <h2 class="cl-header__title">{{ changelog.title }}</h2>
                <p v-if="changelog.subtitle" class="cl-header__subtitle">{{ changelog.subtitle }}</p>
              </div>
            </div>

            <!-- 更新条目 -->
            <ul class="cl-list">
              <li
                v-for="(item, i) in groupedItems"
                :key="i"
                :class="['cl-item', typeConfig[item.type].cls]"
              >
                <span class="cl-item__badge">
                  <i :class="typeConfig[item.type].icon" />
                  {{ typeConfig[item.type].label }}
                </span>
                <span class="cl-item__text">{{ item.text }}</span>
              </li>
            </ul>

            <!-- 底部 -->
            <div class="cl-footer">
              <p class="cl-footer__hint">下次打开不再显示此提示</p>
              <button class="cl-btn" type="button" @click="confirm">
                <i class="i-lucide-check" />
                知道了
              </button>
            </div>
          </div>
        </Transition>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
/* 遮罩 */
.cl-overlay {
  position: fixed;
  inset: 0;
  z-index: 10000;
  background: rgba(0, 0, 0, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
}

/* 对话框 */
.cl-dialog {
  width: 100%;
  max-width: 480px;
  max-height: calc(100vh - 40px);
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 16px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.2);
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

/* 头部 */
.cl-header {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 24px 24px 20px;
  border-bottom: 1px solid var(--color-border);
}

.cl-header__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 44px;
  height: 44px;
  border-radius: 12px;
  background: color-mix(in srgb, var(--color-primary) 12%, transparent);
  color: var(--color-primary);
  font-size: 22px;
  flex-shrink: 0;
}

.cl-header__title {
  font-size: 17px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin: 0 0 2px;
}

.cl-header__subtitle {
  font-size: 13px;
  color: var(--color-text-muted);
  margin: 0;
}

/* 条目列表 */
.cl-list {
  list-style: none;
  margin: 0;
  padding: 16px 24px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  overflow-y: auto;
  flex: 1;
  min-height: 0;
}

.cl-item {
  display: flex;
  gap: 10px;
  align-items: flex-start;
}

.cl-item__badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  border-radius: 6px;
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
  flex-shrink: 0;
  margin-top: 1px;
}

.cl-item__text {
  font-size: 13px;
  color: var(--color-text-primary);
  line-height: 1.6;
}

/* 条目类型配色 */
.type-new .cl-item__badge {
  background: color-mix(in srgb, var(--color-primary) 12%, transparent);
  color: var(--color-primary);
}

.type-fix .cl-item__badge {
  background: color-mix(in srgb, #10b981 12%, transparent);
  color: #10b981;
}

.type-tip .cl-item__badge {
  background: color-mix(in srgb, #f59e0b 12%, transparent);
  color: #d97706;
}

.type-change .cl-item__badge {
  background: color-mix(in srgb, #6b7280 12%, transparent);
  color: #6b7280;
}

/* 底部 */
.cl-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 24px;
  border-top: 1px solid var(--color-border);
  background: color-mix(in srgb, var(--color-bg-elevated) 60%, transparent);
}

.cl-footer__hint {
  font-size: 12px;
  color: var(--color-text-muted);
  margin: 0;
}

.cl-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 8px 20px;
  font-size: 14px;
  font-weight: 600;
  color: #fff;
  background-color: var(--color-primary);
  border: none;
  border-radius: 8px;
  cursor: pointer;
  transition: opacity 0.15s;
}

.cl-btn:hover {
  opacity: 0.88;
}

/* 动画 */
.cl-fade-enter-active,
.cl-fade-leave-active {
  transition: opacity 0.2s;
}

.cl-fade-enter-from,
.cl-fade-leave-to {
  opacity: 0;
}

.cl-slide-enter-active,
.cl-slide-leave-active {
  transition: opacity 0.2s, transform 0.2s;
}

.cl-slide-enter-from,
.cl-slide-leave-to {
  opacity: 0;
  transform: translateY(12px) scale(0.97);
}
</style>

<script setup lang="ts">
import AppButton from '@/components/base/AppButton.vue'
import AppEmpty from '@/components/base/AppEmpty.vue'
import AppBadge from '@/components/base/AppBadge.vue'
import AppDrawer from '@/components/base/AppDrawer.vue'
import AppConfirm from '@/components/base/AppConfirm.vue'
import AppInput from '@/components/base/AppInput.vue'
import AppTextarea from '@/components/base/AppTextarea.vue'
import AppSkeleton from '@/components/base/AppSkeleton.vue'
import { initCharactersState } from './hooks'

const {
  characters,
  loading,
  drawerOpen,
  createForm,
  createLoading,
  openCreate,
  submitCreate,
  deleteTarget,
  deleteLoading,
  openDelete,
  cancelDelete,
  confirmDelete,
  editDrawerOpen,
  editForm,
  editLoading,
  openEdit,
  submitEdit,
} = initCharactersState()
</script>

<template>
  <div class="page">
    <div class="page__header">
      <h2 class="page__title">角色库</h2>
      <AppButton @click="openCreate">
        <i class="i-lucide-plus" />
        添加角色
      </AppButton>
    </div>

    <!-- 骨架屏 -->
    <div v-if="loading" class="char-grid">
      <div v-for="i in 4" :key="i" class="char-skeleton">
        <AppSkeleton width="50%" height="18px" style="margin-bottom: 8px" />
        <AppSkeleton width="30%" height="13px" style="margin-bottom: 10px" />
        <AppSkeleton width="100%" height="13px" />
      </div>
    </div>

    <!-- 空状态 -->
    <AppEmpty
      v-else-if="!characters.length"
      icon="i-lucide-users"
      title="还没有角色"
      description="添加第一个角色，丰富你的故事世界"
    >
      <template #action>
        <AppButton @click="openCreate">
          <i class="i-lucide-plus" />
          添加角色
        </AppButton>
      </template>
    </AppEmpty>

    <!-- 角色卡片网格 -->
    <div v-else class="char-grid">
      <div v-for="char in characters" :key="char.id" class="char-card">
        <div class="char-card__header">
          <div>
            <h3 class="char-name">{{ char.name }}</h3>
            <p v-if="char.role" class="char-role">{{ char.role }}</p>
          </div>
          <div class="char-card__actions">
            <AppBadge v-if="char.age" variant="muted">{{ char.age }} 岁</AppBadge>
            <button class="card-action-btn" title="编辑角色" @click="openEdit(char)">
              <i class="i-lucide-pencil" />
            </button>
            <button class="card-delete-btn" title="删除角色" @click="openDelete(char)">
              <i class="i-lucide-trash-2" />
            </button>
          </div>
        </div>
        <p v-if="char.personalitySummary" class="char-summary">{{ char.personalitySummary }}</p>
        <div v-if="char.tags" class="char-tags">
          <AppBadge
            v-for="tag in char.tags.split(',').filter(Boolean)"
            :key="tag"
            variant="primary"
            size="sm"
          >
            {{ tag.trim() }}
          </AppBadge>
        </div>
      </div>
    </div>

    <!-- 添加角色抽屉 -->
    <AppDrawer v-model="drawerOpen" title="添加角色" width="520px">
      <div class="form-fields">
        <div class="form-section-title">基本信息</div>
        <AppInput v-model="createForm.name" label="角色名称 *" placeholder="角色姓名" />
        <div class="form-row">
          <AppInput v-model="createForm.age" label="年龄" placeholder="如：28" />
          <AppInput v-model="createForm.role" label="身份定位" placeholder="如：主角、反派、导师" />
        </div>

        <div class="form-section-title">性格与动机</div>
        <AppTextarea
          v-model="createForm.personalitySummary"
          label="性格摘要"
          placeholder="用几句话描述角色的核心性格..."
          :rows="3"
        />
        <AppTextarea
          v-model="createForm.motivation"
          label="核心动机"
          placeholder="角色行动背后的驱动力..."
          :rows="2"
        />
        <AppTextarea
          v-model="createForm.speakingStyle"
          label="说话风格"
          placeholder="角色的口头禅、语气特点..."
          :rows="2"
        />

        <div class="form-section-title">约束与状态</div>
        <AppTextarea
          v-model="createForm.forbiddenBehaviors"
          label="禁止行为"
          placeholder="角色绝对不会做的事..."
          :rows="2"
        />
        <AppTextarea
          v-model="createForm.publicSecrets"
          label="公开秘密"
          placeholder="读者/部分角色知道的秘密..."
          :rows="2"
        />
        <AppTextarea
          v-model="createForm.privateSecrets"
          label="隐藏秘密"
          placeholder="只有读者知道的秘密..."
          :rows="2"
        />
        <AppInput
          v-model="createForm.currentState"
          label="当前状态"
          placeholder="如：受伤、被通缉"
        />
        <AppInput
          v-model="createForm.tags"
          label="标签"
          placeholder="多个标签用逗号分隔，如：武者,忠义"
        />
      </div>
      <template #footer>
        <AppButton variant="secondary" @click="drawerOpen = false">取消</AppButton>
        <AppButton
          :loading="createLoading"
          :disabled="!createForm.name.trim()"
          @click="submitCreate"
        >
          保存
        </AppButton>
      </template>
    </AppDrawer>

    <!-- 编辑角色抽屉 -->
    <AppDrawer v-model="editDrawerOpen" title="编辑角色" width="520px">
      <div class="form-fields">
        <div class="form-section-title">基本信息</div>
        <AppInput v-model="editForm.name" label="角色名称 *" placeholder="角色姓名" />
        <div class="form-row">
          <AppInput v-model="editForm.age" label="年龄" placeholder="如：28" />
          <AppInput v-model="editForm.role" label="身份定位" placeholder="如：主角、反派、导师" />
        </div>
        <div class="form-section-title">性格与动机</div>
        <AppTextarea v-model="editForm.personalitySummary" label="性格摘要" :rows="3" />
        <AppTextarea v-model="editForm.motivation" label="核心动机" :rows="2" />
        <AppTextarea v-model="editForm.speakingStyle" label="说话风格" :rows="2" />
        <div class="form-section-title">约束与状态</div>
        <AppTextarea v-model="editForm.forbiddenBehaviors" label="禁止行为" :rows="2" />
        <AppInput v-model="editForm.currentState" label="当前状态" />
        <AppInput v-model="editForm.tags" label="标签" placeholder="多个标签用逗号分隔" />
      </div>
      <template #footer>
        <AppButton variant="secondary" @click="editDrawerOpen = false">取消</AppButton>
        <AppButton :loading="editLoading" :disabled="!editForm.name.trim()" @click="submitEdit">
          保存
        </AppButton>
      </template>
    </AppDrawer>

    <!-- 删除确认 -->
    <AppConfirm
      :model-value="!!deleteTarget"
      title="删除角色"
      :message="`确定删除角色「${deleteTarget?.name}」吗？`"
      variant="danger"
      confirm-text="删除"
      :loading="deleteLoading"
      @update:model-value="cancelDelete"
      @confirm="confirmDelete"
    />
  </div>
</template>

<style scoped>
.page__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
}

.page__title {
  font-size: 20px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}

.char-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 14px;
}

.char-skeleton {
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 16px;
  height: 110px;
}

.char-card {
  background-color: var(--color-bg-surface);
  border: 1px solid var(--color-border);
  border-radius: 10px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  transition: border-color 0.15s;
}

.char-card:hover {
  border-color: var(--color-primary);
}

.char-card:hover .card-delete-btn {
  opacity: 1;
}

.char-card__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 8px;
}

.char-card__actions {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-shrink: 0;
}

.char-name {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0 0 2px;
}

.char-role {
  font-size: 12px;
  color: var(--color-text-muted);
  margin: 0;
}

.char-summary {
  font-size: 13px;
  color: var(--color-text-muted);
  margin: 0;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.char-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.card-delete-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: 6px;
  border: none;
  background: transparent;
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 14px;
  opacity: 0;
  transition:
    opacity 0.15s,
    background-color 0.15s,
    color 0.15s;
}

.card-action-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 26px;
  height: 26px;
  border-radius: 6px;
  border: none;
  background: transparent;
  cursor: pointer;
  color: var(--color-text-muted);
  font-size: 14px;
  opacity: 0;
  transition:
    opacity 0.15s,
    background-color 0.15s,
    color 0.15s;
}

.card-action-btn:hover {
  background-color: color-mix(in srgb, var(--color-primary) 12%, transparent);
  color: var(--color-primary);
}

.char-card:hover .card-action-btn {
  opacity: 1;
}

.card-delete-btn:hover {
  background-color: color-mix(in srgb, var(--color-danger) 12%, transparent);
  color: var(--color-danger);
}

.form-fields {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.form-section-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  padding-top: 6px;
  border-top: 1px solid var(--color-border);
  margin-top: 4px;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}
</style>


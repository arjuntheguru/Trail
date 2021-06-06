import { createSlice, current } from '@reduxjs/toolkit';
import {idle, loading, succeeded, failed} from '../utils/stateStatus';

const initialState = {
    status: idle,    
    taskInfo: {}
}

const taskSlice = createSlice({

    name: 'task',
    initialState: initialState,
    reducers: {
        start_loading: (state) => {
            return {
                ...state,
                status: loading
            }
        },
        load_task_success: (state, action) => {

            return {
                ...state,
                status: succeeded,
                taskInfo: { data: action.payload.data, count: action.payload.totalRecords }                
            }
        },
        add_task_success: (state) => {
            return {
                ...state,
                status: succeeded                
            }
        },
        allocate_task_success: (state) => {
            return {
                ...state,
                status: succeeded                
            }
        },
        update_task_sucess: (state, action) => {

            var task = current(state.taskInfo.data).find(item => item.id === action.payload.data.id);
            const index = current(state.taskInfo.data).indexOf(task);

            var temp = [...current(state.taskInfo.data)]
            console.log(action.payload.data)
            console.log(temp)
            temp.splice(index, 1, action.payload.data);

            var updatedState = {
                
                ...state.taskInfo,
                data: temp,

            }

            return {
                ...state,
                status: succeeded,
                taskInfo: updatedState

            }
        },
        error: (state) => {
            
            return {
                ...state,
                status: failed
            }
        },
        reset: () => initialState
    }

});

export const taskActions = taskSlice.actions;

export default taskSlice.reducer;